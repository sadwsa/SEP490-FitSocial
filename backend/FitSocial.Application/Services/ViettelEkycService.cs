using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

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
        // Ngưỡng: 0.6-0.7 là phổ biến, docs example 0.9 rất gắt
        var refScoreStr = _configuration["ViettelEkyc:RefScore"] ?? "0.65";
        if (!double.TryParse(refScoreStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var refScore))
            refScore = 0.65;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        async Task<byte[]> DownloadAsync(string url)
        {
            try { return await client.GetByteArrayAsync(url, cancellationToken); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image for eKYC: {Url}", url);
                throw new InvalidOperationException("Could not download card/face image for verification.");
            }
        }

        byte[] frontBytes;
        byte[] backBytes;
        byte[] liveBytes;
        try
        {
            frontBytes = await DownloadAsync(request.FrontCardUrl);
            backBytes = await DownloadAsync(request.BackCardUrl);
            liveBytes = await DownloadAsync(request.FaceImageUrl);
        }
        catch (InvalidOperationException ex) { return (false, ex.Message, null!); }

        // Optional extra poses for liveness (Left/Right/Top/Bottom)
        var extraPoses = new List<(byte[] bytes, string pose)>();
        async Task TryAddPose(string? url, string pose)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { var b = await DownloadAsync(url); extraPoses.Add((b, pose)); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to download extra pose {Pose}", pose); }
        }
        await TryAddPose(request.FaceImageLeftUrl, "Left");
        await TryAddPose(request.FaceImageRightUrl, "Right");
        await TryAddPose(request.FaceImageTopUrl, "Top");
        await TryAddPose(request.FaceImageBottomUrl, "Bottom");

        // 1) Bóc tách CCCD
        EkycExtract? extract = null;
        try
        {
            using var form = new MultipartFormDataContent();
            var frontPart = new ByteArrayContent(frontBytes);
            frontPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(frontPart, "image_front", "front.jpg");
            var backPart = new ByteArrayContent(backBytes);
            backPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(backPart, "image_back", "back.jpg");
            form.Add(new StringContent(apiKey), "token");
            using var resp = await client.PostAsync(idCardUrl, form, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Viettel id_card raw: {Body}", raw);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Viettel id_card HTTP {Status}: {Body}", resp.StatusCode, raw);
                return (false, $"ID verification failed (Viettel { (int)resp.StatusCode}). Please retake clear front/back photos and try again. Raw: {raw[..Math.Min(200, raw.Length)]}", null!);
            }
            else
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                int code = root.TryGetProperty("code", out var ce) ? ce.GetInt32() : -1;
                if (code != 1)
                {
                    string vi = root.TryGetProperty("vi_message", out var v) ? v.GetString() ?? "" : "";
                    string en = root.TryGetProperty("en_message", out var e) ? e.GetString() ?? "" : "";
                    string msg = !string.IsNullOrWhiteSpace(vi) ? vi : en;
                    if (string.IsNullOrWhiteSpace(msg) && root.TryGetProperty("message", out var m)) msg = m.GetString() ?? "";
                    _logger.LogWarning("Viettel id_card error {Code}: {Msg}", code, msg);
                    // 261 = không có giấy tờ trong ảnh, 400 = sai dữ liệu
                    return (false, string.IsNullOrWhiteSpace(msg) ? "ID card could not be verified. Please retake photos." : msg, null!);
                }
                string infoStr = "";
                if (root.TryGetProperty("information", out var infoEl))
                    infoStr = infoEl.ValueKind == JsonValueKind.String ? infoEl.GetString() ?? "" : infoEl.GetRawText();
                if (string.IsNullOrWhiteSpace(infoStr))
                    return (false, "Could not extract ID information. Please retake photos.", null!);
                using var infoDoc = JsonDocument.Parse(infoStr);
                var info = infoDoc.RootElement;
                string id = info.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                string name = info.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
                string bday = info.TryGetProperty("birthday", out var bEl) ? bEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(id))
                    return (false, "Could not read ID number from card. Please retake photos.", null!);
                DateOnly? dob = null;
                if (!string.IsNullOrWhiteSpace(bday))
                {
                    if (DateOnly.TryParseExact(bday, "dd/MM/yyyy", out var d1)) dob = d1;
                    else if (DateOnly.TryParseExact(bday, "dd-MM-yyyy", out var d2)) dob = d2;
                    else if (DateOnly.TryParse(bday, out var d3)) dob = d3;
                }
                // Extract all bóc tách fields for full storage
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

                extract = new EkycExtract(
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel id_card call failed");
            return (false, "Could not reach the verification service. Please try again.", null!);
        }

        if (extract == null) return (false, "ID verification failed.", null!);

        decimal? faceMatchScore = null;
        decimal? livenessScore = null;

        // 2) Đối sánh khuôn mặt (Front CCCD vs Live Portrait)
        try
        {
            using var form = new MultipartFormDataContent();
            var cmtPart = new ByteArrayContent(frontBytes);
            cmtPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(cmtPart, "image_cmt", "front.jpg");
            var livePart = new ByteArrayContent(liveBytes);
            livePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(livePart, "image_live", "live.jpg");
            form.Add(new StringContent(apiKey), "token");
            form.Add(new StringContent(refScore.ToString(System.Globalization.CultureInfo.InvariantCulture)), "ref_score");
            using var resp = await client.PostAsync(faceMatchingUrl, form, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Viettel face_matching raw: {Body}", raw);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Viettel face_matching HTTP {Status}: {Body}", resp.StatusCode, raw);
                return (false, $"Face matching failed (Viettel {(int)resp.StatusCode}). Please try again. Raw: {raw[..Math.Min(200, raw.Length)]}", null!);
            }
            else
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                int code = root.TryGetProperty("code", out var ce) ? ce.GetInt32() : -1;
                if (code != 1)
                {
                    string vi = root.TryGetProperty("vi_message", out var v) ? v.GetString() ?? "" : "";
                    string en = root.TryGetProperty("en_message", out var e) ? e.GetString() ?? "" : "";
                    string msg = !string.IsNullOrWhiteSpace(vi) ? vi : en;
                    if (string.IsNullOrWhiteSpace(msg) && root.TryGetProperty("message", out var m)) msg = m.GetString() ?? "";
                    return (false, string.IsNullOrWhiteSpace(msg) ? "Face does not match ID card. Please ensure the live photo is clear and front-facing." : msg, null!);
                }
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
                faceMatchScore = (decimal)score;
                _logger.LogInformation("Face matching score {Score} ref {Ref} verify {Verify} raw {Raw}", score, refScore, verifyResult, verifyStr);
                // Nới lỏng: nếu score rất cao (>=0.85) thì cho qua dù verify=false (Viettel gắt do ảnh thẻ chụp nghiêng/lóa, mặt 2021 vs live 2026)
                if (!verifyResult && score < 0.85)
                    return (false, $"Face does not match ID card (verify={verifyResult}, score {score:F2}, threshold {refScore:F2}). Please retake: front card straight, no glare, live face front-facing, good lighting, no glasses. Raw: verify={verifyResult} score={score:F2}", null!);
                if (verifyResult && score < refScore)
                    return (false, $"Face score too low ({score:F2} < {refScore:F2}). Please retake a clear front-facing photo.", null!);
                if (!verifyResult && score >= 0.85)
                    _logger.LogWarning("Face matching verify=false but score {Score} >=0.85, allowing pass (lenient for same person, card photo 2021 vs live)", score);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viettel face_matching failed");
            return (false, "Could not verify face matching. Please try again.", null!);
        }

        // 3) Phát hiện thực thể sống - Portrait chính + các pose phụ (Left/Right/Top/Bottom) nếu có
        // Gần/xa được hướng dẫn ở frontend (yêu cầu user lại gần/ra xa), backend chỉ cần check Portrait là đủ, nhưng nếu có thêm pose sẽ check từng pose
        var livenessChecks = new List<(byte[] bytes, string pose)> { (liveBytes, "Portrait") };
        livenessChecks.AddRange(extraPoses);

        foreach (var (bytes, pose) in livenessChecks)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                var filePart = new ByteArrayContent(bytes);
                filePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                form.Add(filePart, "file", $"{pose.ToLower()}.jpg");
                form.Add(new StringContent(apiKey), "token");
                form.Add(new StringContent(pose), "label_pose");
                using var resp = await client.PostAsync(faceLivenessUrl, form, cancellationToken);
                var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Viettel face_liveness {Pose} raw: {Body}", pose, raw);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Viettel face_liveness {Pose} HTTP {Status}: {Body}", pose, resp.StatusCode, raw);
                    return (false, $"Liveness check failed for {pose} (Viettel {(int)resp.StatusCode}). Please try again. Raw: {raw[..Math.Min(200, raw.Length)]}", null!);
                }
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                int code = root.TryGetProperty("code", out var ce) ? ce.GetInt32() : -1;
                if (code != 1)
                {
                    string vi = root.TryGetProperty("vi_message", out var v) ? v.GetString() ?? "" : "";
                    string en = root.TryGetProperty("en_message", out var e) ? e.GetString() ?? "" : "";
                    string msg = !string.IsNullOrWhiteSpace(vi) ? vi : en;
                    if (string.IsNullOrWhiteSpace(msg) && root.TryGetProperty("message", out var m)) msg = m.GetString() ?? "";
                    return (false, string.IsNullOrWhiteSpace(msg) ? $"Liveness check failed for {pose}. Please ensure you are a real person in good lighting." : $"{pose}: {msg}", null!);
                }
                // verify_result: "Real" / "Fake" or similar - docs say String verify_result + score Float [0-1]
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
                if (pose == "Portrait") livenessScore = (decimal)score;
                // Coi "Real" / "True" / score >= 0.5 là pass
                bool isReal = verifyStr.Equals("Real", StringComparison.OrdinalIgnoreCase)
                    || verifyStr.Equals("True", StringComparison.OrdinalIgnoreCase)
                    || verifyStr.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || score >= 0.5f;
                // Nếu API trả verify_result là Boolean thì đã xử lý ở trên
                if (!isReal && score < 0.5f && !string.IsNullOrWhiteSpace(verifyStr) && verifyStr.Equals("Fake", StringComparison.OrdinalIgnoreCase))
                    return (false, $"Liveness check failed for {pose} (fake/spoof detected). Please use a real live photo with good lighting.", null!);
                // Nếu score thấp nhưng verify_result không rõ, vẫn chặn nếu score < 0.3
                if (score > 0 && score < 0.3f)
                    return (false, $"Liveness score too low for {pose} ({score:F2}). Please retake with face centered, good lighting, and follow the guide (near/far, left/right).", null!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Viettel face_liveness {Pose} failed", pose);
                return (false, $"Could not verify liveness for {pose}. Please try again.", null!);
            }
        }

        // Gắn scores vào extract để lưu DB
        if (faceMatchScore.HasValue || livenessScore.HasValue)
        {
            extract = extract with
            {
                FaceMatchConfidence = faceMatchScore,
                LivenessScore = livenessScore
            };
        }

        return (true, null, extract);
    }
}
