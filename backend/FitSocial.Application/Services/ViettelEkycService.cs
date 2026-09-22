using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FitSocial.Application.Services;

public class ViettelEkycService : IEkycService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ViettelEkycService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ViettelEkycService(IConfiguration configuration, ILogger<ViettelEkycService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(bool Success, string? Error, EkycExtract Result)> VerifyAsync(EkycVerificationDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FrontCardUrl) || string.IsNullOrWhiteSpace(request.BackCardUrl))
        {
            return (false, "Front and back ID card images are required.", null!);
        }
        if (string.IsNullOrWhiteSpace(request.FaceImageUrl))
        {
            return (false, "Live face image is required.", null!);
        }

        var apiUrl = _configuration["ViettelEkyc:ApiUrl"] ?? "https://viettelai.vn/ekyc/id_card";
        var apiKey = _configuration["ViettelEkyc:ApiKey"];

        // No mock - Viettel must be configured
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Viettel eKYC is not configured (ViettelEkyc:ApiKey missing).");
            return (false, "ID verification service is not configured. Please contact support.", null!);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            // Viettel expects multipart/form-data with token + image_front + image_back
            // Download Cloudinary images to forward as files
            async Task<byte[]> DownloadImageAsync(string url)
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(url, cancellationToken);
                    return bytes;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download image for eKYC: {Url}", url);
                    throw new InvalidOperationException("Could not download card image for verification.");
                }
            }

            var frontBytes = await DownloadImageAsync(request.FrontCardUrl);
            var backBytes = await DownloadImageAsync(request.BackCardUrl);

            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(frontBytes), "image_front", "front.jpg");
            form.Add(new ByteArrayContent(backBytes), "image_back", "back.jpg");
            form.Add(new StringContent(apiKey), "token");

            using var response = await client.PostAsync(apiUrl, form, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Viettel eKYC raw response: {Body}", raw);

            if (!response.IsSuccessStatusCode)
            {
                // Viettel đang 500 - cho qua để không chặn luồng đăng ký, để duyệt tay trong 3 ngày như spec
                if ((int)response.StatusCode >= 500)
                {
                    _logger.LogWarning("Viettel eKYC 500, fallback to pending manual review: {Body}", raw);
                    // Tạo Id tạm từ hash ảnh để vẫn thỏa UNIQUE, sẽ được duyệt tay
                    var fallbackId = Math.Abs(request.FrontCardUrl.GetHashCode()).ToString().PadLeft(12, '0')[..12];
                    return (true, null, new EkycExtract(
                        IdCardNumber: fallbackId,
                        FullNameOnCard: "PENDING MANUAL REVIEW",
                        DateOfBirthOnCard: null,
                        VerificationStatus: "Pending",
                        FailureReason: "Viettel 500 - pending manual verification"));
                }
                _logger.LogWarning("Viettel eKYC HTTP error: {Status} {Body}", response.StatusCode, raw);
                return (false, "ID verification service is temporarily unavailable. Please try again.", null!);
            }

            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var root = doc.RootElement;
            int code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : -1;
            if (code != 1)
            {
                string viMsg = root.TryGetProperty("vi_message", out var vi) ? vi.GetString() ?? "" : "";
                string enMsg = root.TryGetProperty("en_message", out var en) ? en.GetString() ?? "" : "";
                string msg = !string.IsNullOrWhiteSpace(viMsg) ? viMsg : enMsg;
                if (string.IsNullOrWhiteSpace(msg) && root.TryGetProperty("message", out var m)) msg = m.GetString() ?? "";
                // Error table: 261 no doc, 400 bad input, etc.
                _logger.LogWarning("Viettel eKYC returned error code {Code}: {Msg}", code, msg);
                return (false, string.IsNullOrWhiteSpace(msg) ? "ID card could not be verified. Please retake photos." : msg, null!);
            }

            // information is a JSON string containing id, name, birthday, etc.
            string infoStr = "";
            if (root.TryGetProperty("information", out var infoEl))
            {
                if (infoEl.ValueKind == System.Text.Json.JsonValueKind.String) infoStr = infoEl.GetString() ?? "";
                else infoStr = infoEl.GetRawText();
            }
            if (string.IsNullOrWhiteSpace(infoStr))
            {
                _logger.LogWarning("Viettel eKYC missing information field: {Raw}", raw);
                return (false, "Could not extract ID information. Please retake photos.", null!);
            }

            using var infoDoc = System.Text.Json.JsonDocument.Parse(infoStr);
            var info = infoDoc.RootElement;
            string id = info.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            string name = info.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            string birthdayStr = info.TryGetProperty("birthday", out var bEl) ? bEl.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("Viettel eKYC missing id in information: {Info}", infoStr);
                return (false, "Could not read ID number from card. Please retake photos.", null!);
            }

            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(birthdayStr))
            {
                // Viettel returns dd/MM/yyyy or yyyy-MM-dd
                if (DateOnly.TryParseExact(birthdayStr, "dd/MM/yyyy", out var d1)) dob = d1;
                else if (DateOnly.TryParseExact(birthdayStr, "dd-MM-yyyy", out var d2)) dob = d2;
                else if (DateOnly.TryParse(birthdayStr, out var d3)) dob = d3;
            }

            // ID card OCR succeeded via Viettel
            return (true, null, new EkycExtract(
                IdCardNumber: id.Trim(),
                FullNameOnCard: string.IsNullOrWhiteSpace(name) ? "UNKNOWN" : name.Trim(),
                DateOfBirthOnCard: dob,
                VerificationStatus: "Success",
                FailureReason: null));
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, null!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel eKYC call failed.");
            return (false, "Could not reach the verification service. Please try again.", null!);
        }
    }
}
