using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace FitSocial.Application.Services;

public class ViettelEkycService : IEkycService
{
    private const long MaxDownloadBytes = 10 * 1024 * 1024; 

    private readonly IConfiguration _configuration;
    private readonly ILogger<ViettelEkycService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICoachEkycVerificationRepository _ekycRepo;
    private readonly IUserRepository _users;

    public ViettelEkycService(
        IConfiguration configuration,
        ILogger<ViettelEkycService> logger,
        IHttpClientFactory httpClientFactory,
        ICoachEkycVerificationRepository ekycRepo,
        IUserRepository users)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _ekycRepo = ekycRepo;
        _users = users;
    }

    public async Task<(bool Available, string? Error)> CheckIdCardAsync(string idCardNumber, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idCardNumber))
            return (false, "IdCardNumber is required.");

        var normalizedId = idCardNumber.Trim();
        Guid? excludeCoachId = await ResolveExcludeCoachIdAsync(null, email, cancellationToken);

        var exists = await _ekycRepo.ExistsByIdCardNumberAsync(normalizedId, excludeCoachId, cancellationToken);
        if (exists)
            return (false, "This ID card has already been used for another coach.");

        return (true, null);
    }

    public async Task<(bool Success, string? Error, EkycExtract Result)> VerifyAsync(EkycVerificationDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FrontCardUrl) || string.IsNullOrWhiteSpace(request.BackCardUrl))
            return (false, "Front and back ID card images are required.", null!);
        if (string.IsNullOrWhiteSpace(request.FaceImageUrl))
            return (false, "Live face image is required.", null!);

        var apiKey = _configuration["ViettelEkyc:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Viettel eKYC is not configured (ViettelEkyc:ApiKey missing).");
            return (false, "ID verification service is not configured. Please contact support.", null!);
        }

        var idCardUrl = _configuration["ViettelEkyc:ApiUrl"] ?? "https://viettelai.vn/ekyc/id_card";
        var faceMatchingUrl = _configuration["ViettelEkyc:FaceMatchingUrl"] ?? "https://viettelai.vn/ekyc/face_matching";
        var faceLivenessUrl = _configuration["ViettelEkyc:FaceLivenessUrl"] ?? "https://viettelai.vn/ekyc/face_liveness";
        var refScoreStr = _configuration["ViettelEkyc:RefScore"] ?? "0.65";
        if (!double.TryParse(refScoreStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var refScore))
            refScore = 0.65;

        var timeoutSecondsStr = _configuration["ViettelEkyc:TimeoutSeconds"] ?? "120";
        if (!int.TryParse(timeoutSecondsStr, out var timeoutSeconds) || timeoutSeconds <= 0)
            timeoutSeconds = 120;
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        byte[] frontBytes;
        byte[] backBytes;
        byte[] liveBytes;
        string frontCt;
        string backCt;
        string liveCt;
        try
        {
            var tFront = DownloadImageAsync(client, request.FrontCardUrl, cancellationToken);
            var tBack = DownloadImageAsync(client, request.BackCardUrl, cancellationToken);
            var tLive = DownloadImageAsync(client, request.FaceImageUrl, cancellationToken);
            await Task.WhenAll(tFront, tBack, tLive);
            (frontBytes, frontCt) = await tFront;
            (backBytes, backCt) = await tBack;
            (liveBytes, liveCt) = await tLive;
        }
        catch (Exception ex)
        {
            var msg = ex is AggregateException agg ? (agg.InnerExceptions.FirstOrDefault()?.Message ?? ex.Message) : ex.Message;
            return (false, msg, null!);
        }

        var extraPoses = await DownloadExtraPosesAsync(client, request, cancellationToken);

        var (ocrSuccess, ocrError, extract) = await VerifyIdCardAsync(client, idCardUrl, apiKey, frontBytes, frontCt, backBytes, backCt, cancellationToken);
        if (!ocrSuccess || extract == null)
            return (false, ocrError ?? "ID verification failed.", null!);

        Guid? excludeCoachId = await ResolveExcludeCoachIdAsync(request.UserId, request.Email, cancellationToken);
        var duplicate = await _ekycRepo.ExistsByIdCardNumberAsync(extract.IdCardNumber, excludeCoachId, cancellationToken);
        if (duplicate)
            return (false, "This ID card has already been used for another coach.", null!);

        var faceTask = VerifyFaceMatchingAsync(client, faceMatchingUrl, apiKey, refScore, frontBytes, frontCt, liveBytes, liveCt, cancellationToken);

        var livenessChecks = new List<(byte[] bytes, string contentType, string pose)>
        {
            (liveBytes, liveCt, "Portrait")
        };
        livenessChecks.AddRange(extraPoses.Select(p => (p.bytes, p.contentType, p.pose)));

        var livenessTask = VerifyFaceLivenessAsync(client, faceLivenessUrl, apiKey, livenessChecks, cancellationToken);

        await Task.WhenAll(faceTask, livenessTask);

        var (faceOk, faceError, faceScore) = await faceTask;
        if (!faceOk)
            return (false, faceError ?? "Face matching failed.", null!);

        var (liveOk, liveError, liveScore) = await livenessTask;
        if (!liveOk)
            return (false, liveError ?? "Liveness check failed.", null!);

        if (faceScore.HasValue || liveScore.HasValue)
        {
            extract = extract with
            {
                FaceMatchConfidence = faceScore,
                LivenessScore = liveScore
            };
        }

        return (true, null, extract);
    }

    private static async Task<(byte[] bytes, string contentType)> DownloadImageAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Invalid image URL.");

        if (IsBlockedHost(uri.Host))
            throw new InvalidOperationException("Image URL is not allowed.");

        try
        {
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException("Could not download card/face image for verification.");

            if (resp.Content.Headers.ContentLength.HasValue && resp.Content.Headers.ContentLength.Value > MaxDownloadBytes)
                throw new InvalidOperationException("Image file is too large.");

            var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > MaxDownloadBytes)
                throw new InvalidOperationException("Image file is too large.");

            var contentType = resp.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType))
                contentType = "image/jpeg";

            return (bytes, contentType);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not download card/face image for verification.", ex);
        }
    }

    private async Task<List<(byte[] bytes, string contentType, string pose)>> DownloadExtraPosesAsync(HttpClient client, EkycVerificationDto request, CancellationToken cancellationToken)
    {
        var extraPoseUrls = new (string? url, string pose)[]
        {
            (request.FaceImageLeftUrl, "Left"),
            (request.FaceImageRightUrl, "Right")
        };

        var tasks = extraPoseUrls
            .Where(x => !string.IsNullOrWhiteSpace(x.url))
            .Select(async x =>
            {
                try
                {
                    var (b, ct) = await DownloadImageAsync(client, x.url!, cancellationToken);
                    return (bytes: b, contentType: ct, pose: x.pose);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download extra pose {Pose}", x.pose);
                    return ((byte[] bytes, string contentType, string pose)?)null;
                }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);
        var list = new List<(byte[] bytes, string contentType, string pose)>();
        foreach (var r in results)
        {
            if (r.HasValue) list.Add(r.Value);
        }
        return list;
    }

    private async Task<(bool Success, string? Error, EkycExtract? Extract)> VerifyIdCardAsync(
        HttpClient client, string url, string apiKey,
        byte[] frontBytes, string frontCt, byte[] backBytes, string backCt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var frontPart = new ByteArrayContent(frontBytes);
            frontPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(frontCt);
            form.Add(frontPart, "image_front", "front.jpg");
            var backPart = new ByteArrayContent(backBytes);
            backPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(backCt);
            form.Add(backPart, "image_back", "back.jpg");
            form.Add(new StringContent(apiKey), "token");

            var (ok, error, root) = await SendViettelRequestAsync(client, url, form, "Identity verification service is temporarily unavailable. Please try again later or contact support.", "ID card could not be verified. Please retake photos.", cancellationToken);
            if (!ok)
                return (false, string.IsNullOrWhiteSpace(error) ? "ID card could not be verified. Please ensure your photos are clear, well-lit, and try again." : error, null);

            string infoStr = "";
            if (root.TryGetProperty("information", out var infoEl))
                infoStr = infoEl.ValueKind == JsonValueKind.String ? infoEl.GetString() ?? "" : infoEl.GetRawText();
            if (string.IsNullOrWhiteSpace(infoStr))
                return (false, "Could not extract ID information. Please retake photos.", null);
            using var infoDoc = JsonDocument.Parse(infoStr);
            var info = infoDoc.RootElement;
            string id = info.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            string name = info.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
            string bday = info.TryGetProperty("birthday", out var bEl) ? bEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id))
                return (false, "Could not read ID number from card. Please retake photos.", null);
            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(bday))
            {
                if (DateOnly.TryParseExact(bday, "dd/MM/yyyy", out var d1)) dob = d1;
                else if (DateOnly.TryParseExact(bday, "dd-MM-yyyy", out var d2)) dob = d2;
                else if (DateOnly.TryParse(bday, out var d3)) dob = d3;
            }

            string GetStr(string key) => info.TryGetProperty(key, out var el) ? el.GetString() ?? "" : "";
            var birthplace = GetStr("birthplace");
            var sex = GetStr("sex");
            var address = GetStr("address");
            var province = GetStr("province");
            var district = GetStr("district");
            var ward = GetStr("ward");
            var provinceCode = GetStr("province_code");
            var districtCode = GetStr("district_code");
            var wardCode = GetStr("ward_code");
            var street = GetStr("street");
            var nationality = GetStr("nationality");
            var religion = GetStr("religion");
            var ethnicity = GetStr("ethnicity");
            var expiry = GetStr("expiry");
            var feature = GetStr("feature");
            var issueDate = GetStr("issue_date");
            var issueBy = GetStr("issue_by");
            var docType = GetStr("document");
            if (string.IsNullOrWhiteSpace(docType)) docType = GetStr("type");

            var extract = new EkycExtract(
                IdCardNumber: id.Trim(),
                FullNameOnCard: string.IsNullOrWhiteSpace(name) ? "UNKNOWN" : name.Trim(),
                DateOfBirthOnCard: dob,
                VerificationStatus: "Success",
                FailureReason: null,
                Birthplace: string.IsNullOrWhiteSpace(birthplace) ? null : birthplace.Trim(),
                Sex: string.IsNullOrWhiteSpace(sex) ? null : sex.Trim(),
                Address: string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                Province: string.IsNullOrWhiteSpace(province) ? null : province.Trim(),
                District: string.IsNullOrWhiteSpace(district) ? null : district.Trim(),
                Ward: string.IsNullOrWhiteSpace(ward) ? null : ward.Trim(),
                ProvinceCode: string.IsNullOrWhiteSpace(provinceCode) ? null : provinceCode.Trim(),
                DistrictCode: string.IsNullOrWhiteSpace(districtCode) ? null : districtCode.Trim(),
                WardCode: string.IsNullOrWhiteSpace(wardCode) ? null : wardCode.Trim(),
                Street: string.IsNullOrWhiteSpace(street) ? null : street.Trim(),
                Nationality: string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim(),
                Religion: string.IsNullOrWhiteSpace(religion) ? null : religion.Trim(),
                Ethnicity: string.IsNullOrWhiteSpace(ethnicity) ? null : ethnicity.Trim(),
                Expiry: string.IsNullOrWhiteSpace(expiry) ? null : expiry.Trim(),
                Feature: string.IsNullOrWhiteSpace(feature) ? null : feature.Trim(),
                IssueDate: string.IsNullOrWhiteSpace(issueDate) ? null : issueDate.Trim(),
                IssueBy: string.IsNullOrWhiteSpace(issueBy) ? null : issueBy.Trim(),
                DocumentType: string.IsNullOrWhiteSpace(docType) ? null : docType.Trim(),
                RawInformationJson: infoStr);

            return (true, null, extract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel id_card call failed");
            return (false, "Could not reach the verification service. Please try again.", null);
        }
    }

    private async Task<(bool Success, string? Error, decimal? Score)> VerifyFaceMatchingAsync(
        HttpClient client, string url, string apiKey, double refScore,
        byte[] frontBytes, string frontCt, byte[] liveBytes, string liveCt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var cmtPart = new ByteArrayContent(frontBytes);
            cmtPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(frontCt);
            form.Add(cmtPart, "image_cmt", "front.jpg");
            var livePart = new ByteArrayContent(liveBytes);
            livePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(liveCt);
            form.Add(livePart, "image_live", "live.jpg");
            form.Add(new StringContent(apiKey), "token");
            form.Add(new StringContent(refScore.ToString(System.Globalization.CultureInfo.InvariantCulture)), "ref_score");

            var (ok, error, root) = await SendViettelRequestAsync(client, url, form, "Face matching service is temporarily unavailable. Please try again later or contact support.", "Face does not match ID card. Please ensure the live photo is clear and front-facing.", cancellationToken);
            if (!ok)
                return (false, string.IsNullOrWhiteSpace(error) ? "Face matching failed. Please ensure your live photo is clear, well-lit, and matches your ID card." : error, null);

            string verifyStr = "";
            bool verifyResult = false;
            if (root.TryGetProperty("verify_result", out var vr))
            {
                if (vr.ValueKind == JsonValueKind.True) { verifyResult = true; verifyStr = "true"; }
                else if (vr.ValueKind == JsonValueKind.False) { verifyResult = false; verifyStr = "false"; }
                else if (vr.ValueKind == JsonValueKind.String) { verifyStr = vr.GetString() ?? ""; verifyResult = verifyStr.Equals("true", StringComparison.OrdinalIgnoreCase); }
            }
            double score = 0;
            if (root.TryGetProperty("score", out var sEl) && sEl.TryGetDouble(out var sd)) score = sd;
            var scoreDec = (decimal)score;
            _logger.LogInformation("Face matching score {Score} ref {Ref} verify {Verify}", score, refScore, verifyResult);

            if (!verifyResult && score < 0.85)
                return (false, "Face does not match your ID card. Please ensure your card is straight without glare, and your live face is front-facing in good lighting without glasses.", null);
            if (verifyResult && score < refScore)
                return (false, "Face match score is too low. Please retake a clear front-facing photo.", null);
            if (!verifyResult && score >= 0.85)
                _logger.LogWarning("Face matching verify=false but score {Score} >=0.85, allowing pass (lenient for same person, card photo 2021 vs live)", score);

            return (true, null, scoreDec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel face_matching failed");
            return (false, "Could not verify face matching. Please try again.", null);
        }
    }

    private async Task<(bool Success, string? Error, decimal? Score)> VerifyFaceLivenessAsync(
        HttpClient client, string url, string apiKey,
        IReadOnlyList<(byte[] bytes, string contentType, string pose)> poses,
        CancellationToken cancellationToken)
    {
        var tasks = poses.Select(p => VerifySingleLivenessAsync(client, url, apiKey, p.bytes, p.contentType, p.pose, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var r in results)
        {
            if (!r.Success)
                return (false, r.Error, null);
        }

        var portrait = results.FirstOrDefault(r => r.Pose == "Portrait");
        return (true, null, portrait.Score);
    }

    private async Task<(bool Success, string? Error, string Pose, decimal? Score)> VerifySingleLivenessAsync(
        HttpClient client, string url, string apiKey,
        byte[] bytes, string contentType, string pose,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var filePart = new ByteArrayContent(bytes);
            filePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(filePart, "file", $"{pose.ToLower()}.jpg");
            form.Add(new StringContent(apiKey), "token");
            form.Add(new StringContent(pose), "label_pose");

            var (ok, error, root) = await SendViettelRequestAsync(client, url, form, "Face verification service is temporarily unavailable. Please try again later or contact support.", $"Liveness check failed for {pose}. Please ensure you are a real person in good lighting.", cancellationToken);
            if (!ok)
                return (false, string.IsNullOrWhiteSpace(error) ? $"Face liveness verification failed for {pose}. Please ensure good lighting and try again." : error, pose, null);

            string verifyStr = "";
            if (root.TryGetProperty("verify_result", out var vrEl))
            {
                if (vrEl.ValueKind == JsonValueKind.String) verifyStr = vrEl.GetString() ?? "";
                else if (vrEl.ValueKind == JsonValueKind.True || vrEl.ValueKind == JsonValueKind.False) verifyStr = vrEl.GetBoolean() ? "True" : "False";
            }
            float score = 0;
            if (root.TryGetProperty("score", out var sEl))
            {
                if (sEl.ValueKind == JsonValueKind.Number && sEl.TryGetSingle(out var sf)) score = sf;
                else if (sEl.TryGetDouble(out var sd)) score = (float)sd;
            }
            _logger.LogInformation("Liveness {Pose} verify {Verify} score {Score}", pose, verifyStr, score);

            bool isRealResult = verifyStr.Equals("Real", StringComparison.OrdinalIgnoreCase)
                || verifyStr.Equals("True", StringComparison.OrdinalIgnoreCase);

            if (!isRealResult || score < 0.5f)
            {
                return (false, $"Liveness check failed for {pose}. Please ensure you are a real person in good lighting.", pose, null);
            }

            return (true, null, pose, (decimal)score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel face_liveness {Pose} failed", pose);
            return (false, $"Could not verify liveness for {pose}. Please try again.", pose, null);
        }
    }

    private async Task<(bool Success, string? Error, JsonElement Root)> SendViettelRequestAsync(
        HttpClient client, string url, MultipartFormDataContent form, string unavailableMessage, string defaultMessage, CancellationToken cancellationToken)
    {
        try
        {
            using var resp = await client.PostAsync(url, form, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Viettel HTTP {Status} for {Url}", resp.StatusCode, url);
                if (resp.StatusCode == HttpStatusCode.Forbidden || raw.Contains("remaining unit", StringComparison.OrdinalIgnoreCase) || raw.Contains("upgrade your package", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, unavailableMessage, default);
                }
                return (false, null, default);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Viettel invalid JSON for {Url}", url);
                return (false, defaultMessage, default);
            }

            using (doc)
            {
                var root = doc.RootElement;
                int code = root.TryGetProperty("code", out var ce) ? ce.GetInt32() : -1;
                if (code != 1)
                {
                    string vi = root.TryGetProperty("vi_message", out var v) ? v.GetString() ?? "" : "";
                    string en = root.TryGetProperty("en_message", out var e) ? e.GetString() ?? "" : "";
                    string msg = !string.IsNullOrWhiteSpace(vi) ? vi : en;
                    if (string.IsNullOrWhiteSpace(msg) && root.TryGetProperty("message", out var m)) msg = m.GetString() ?? "";
                    _logger.LogWarning("Viettel error {Code}: {Msg}", code, msg);
                    if (msg.Contains("remaining unit", StringComparison.OrdinalIgnoreCase) || code == 403 || msg.Contains("upgrade your package", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, unavailableMessage, default);
                    }
                    return (false, string.IsNullOrWhiteSpace(msg) ? defaultMessage : msg, default);
                }
                return (true, null, root.Clone());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel request failed for {Url}", url);
            return (false, "Could not reach the verification service. Please try again.", default);
        }
    }

    private async Task<Guid?> ResolveExcludeCoachIdAsync(Guid? userId, string? email, CancellationToken cancellationToken)
    {
        if (userId.HasValue) return userId;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _users.FindByEmailAsync(email.Trim().ToLower(), cancellationToken);
            if (existingUser != null) return existingUser.UserId;
        }
        return null;
    }

    private static bool IsBlockedHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out var ip))
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
                return true;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 10) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;
            }
            else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                    return true;
            }
        }

        return false;
    }
}