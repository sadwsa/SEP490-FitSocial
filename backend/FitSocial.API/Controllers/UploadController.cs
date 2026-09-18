using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FitSocial.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10MB (coach credentials)

    private readonly IConfiguration _configuration;
    private readonly ILogger<UploadController> _logger;
    private readonly Cloudinary? _cloudinary;

    public UploadController(
        IConfiguration configuration,
        ILogger<UploadController> logger,
        Cloudinary? cloudinary = null)
    {
        _configuration = configuration;
        _logger = logger;
        _cloudinary = cloudinary;
    }

    /// <summary>
    /// Upload coach credential files (ID card images, certificates PDF/PNG/JPG, max 10MB) to Cloudinary.
    /// Returns the secure URL of the stored file.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(MaxFileBytes)]
    [ProducesResponseType(typeof(ApiResponseDto<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponseDto<string>.Fail("Please choose a file to upload."));
        }

        if (file.Length > MaxFileBytes)
        {
            return BadRequest(ApiResponseDto<string>.Fail("File size must not exceed 10MB."));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(ApiResponseDto<string>.Fail("Only JPG, PNG images and PDF files are allowed."));
        }

        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            return BadRequest(ApiResponseDto<string>.Fail("File upload is not configured on the server."));
        }

        try
        {
            var cloudinary = _cloudinary ?? new Cloudinary(new Account(cloudName, apiKey, apiSecret));

            await using var stream = file.OpenReadStream();
            UploadResult uploadResult;

            if (extension == ".pdf")
            {
                uploadResult = await cloudinary.UploadAsync(new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "fitsocial/credentials",
                    UseFilename = true,
                    UniqueFilename = true
                });
            }
            else
            {
                uploadResult = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "fitsocial/credentials",
                    UseFilename = true,
                    UniqueFilename = true
                });
            }

            if (uploadResult.Error != null || string.IsNullOrWhiteSpace(uploadResult.SecureUrl?.ToString()))
            {
                return BadRequest(ApiResponseDto<string>.Fail(
                    $"Upload failed: {uploadResult.Error?.Message ?? "Unknown Cloudinary error"}"));
            }

            return Ok(ApiResponseDto<string>.Ok(
                uploadResult.SecureUrl.ToString(), "File uploaded successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponseDto<string>.Fail($"Upload failed: {ex.Message}"));
        }
    }

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] AllowedVideoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
    private const long MaxImageBytes = 10 * 1024 * 1024; // 10MB
    private const long MaxVideoBytes = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Upload media for a post (max 10 files, accepts both images and videos).
    /// Returns a list of { mediaUrl, mediaType } to supply to the create post endpoint.
    /// </summary>
    [HttpPost("post-media")]
    [AllowAnonymous]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPostMedia([FromForm] List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        if (files == null || files.Count == 0)
        {
            return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail("Please select at least one file to upload."));
        }

        if (files.Count > 10)
        {
            return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail("A post can have at most 10 media files (photos or videos)."));
        }

        long totalIncomingBytes = files.Sum(f => f.Length);
        _logger.LogInformation("[API][UploadPostMedia] Received {Count} files, total size: {SizeMb:F2} MB",
            files.Count, totalIncomingBytes / (1024.0 * 1024.0));

        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail("File storage service is not configured on the server."));
        }

        // Validate all files format and size up front before uploading
        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isImage = AllowedImageExtensions.Contains(extension);
            var isVideo = AllowedVideoExtensions.Contains(extension);

            if (!isImage && !isVideo)
            {
                return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail(
                    $"Unsupported file format for '{file.FileName}'. Only images (JPG, PNG, WEBP, GIF) and videos (MP4, MOV, AVI, MKV, WEBM) are supported."));
            }

            if (isImage && file.Length > MaxImageBytes)
            {
                return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail(
                    $"Image '{file.FileName}' exceeds the maximum allowed size (10MB)."));
            }

            if (isVideo && file.Length > MaxVideoBytes)
            {
                return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail(
                    $"Video '{file.FileName}' exceeds the maximum allowed size (100MB)."));
            }
        }

        var cloudinary = _cloudinary ?? new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        var uploadedMedia = new FitSocial.Application.DTOs.Posts.CreatePostMediaDto[files.Count];

        // Configurable concurrency with safe default and clamping (default: 10, range: 1..10)
        var configuredConcurrency = _configuration.GetValue<int?>("Cloudinary:UploadConcurrency") ?? 10;
        var uploadConcurrency = Math.Clamp(configuredConcurrency, 1, 10);

        _logger.LogInformation("[API][UploadPostMedia] Starting Cloudinary upload with concurrency: {Concurrency}", uploadConcurrency);
        using var throttler = new SemaphoreSlim(uploadConcurrency, uploadConcurrency);

        var cloudinarySw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var uploadTasks = files.Select(async (file, index) =>
            {
                await throttler.WaitAsync(cancellationToken);
                var fileSw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var isVideo = AllowedVideoExtensions.Contains(extension);

                    await using var stream = file.OpenReadStream();
                    UploadResult uploadResult;

                    if (isVideo)
                    {
                        var videoParams = new VideoUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "fitsocial/posts/videos",
                            UseFilename = false,
                            UniqueFilename = true
                        };

                        if (file.Length > 20 * 1024 * 1024)
                        {
                            uploadResult = await cloudinary.UploadLargeAsync(videoParams, bufferSize: 20 * 1024 * 1024, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            uploadResult = await cloudinary.UploadAsync(videoParams, cancellationToken);
                        }
                    }
                    else
                    {
                        uploadResult = await cloudinary.UploadAsync(new ImageUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "fitsocial/posts/images",
                            UseFilename = false,
                            UniqueFilename = true
                        }, cancellationToken);
                    }

                    if (uploadResult.Error != null || string.IsNullOrWhiteSpace(uploadResult.SecureUrl?.ToString()))
                    {
                        throw new InvalidOperationException($"Failed to upload '{file.FileName}': {uploadResult.Error?.Message ?? "Unknown Cloudinary error"}");
                    }

                    // Preserve original media ordering
                    uploadedMedia[index] = new FitSocial.Application.DTOs.Posts.CreatePostMediaDto
                    {
                        MediaUrl = uploadResult.SecureUrl.ToString(),
                        MediaType = isVideo ? "VIDEO" : "IMAGE"
                    };

                    _logger.LogInformation("[Cloudinary] File #{Index} '{FileName}' ({SizeKb:F0} KB) uploaded in {Duration} ms",
                        index + 1, file.FileName, file.Length / 1024.0, fileSw.ElapsedMilliseconds);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(uploadTasks);
            cloudinarySw.Stop();

            totalSw.Stop();
            var nonCloudinaryMs = totalSw.ElapsedMilliseconds - cloudinarySw.ElapsedMilliseconds;

            _logger.LogInformation(
                "[API][UploadPostMedia] Completed. Total duration: {TotalMs} ms | Cloudinary duration: {CloudinaryMs} ms | Other/Buffering: {OtherMs} ms",
                totalSw.ElapsedMilliseconds, cloudinarySw.ElapsedMilliseconds, nonCloudinaryMs);

            return Ok(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Ok(
                uploadedMedia.ToList(), "Post media uploaded successfully."));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[API][UploadPostMedia] Upload cancelled by client after {TotalMs} ms", totalSw.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status499ClientClosedRequest, ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail("Upload cancelled by client."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API][UploadPostMedia] Upload failed after {TotalMs} ms: {Message}", totalSw.ElapsedMilliseconds, ex.Message);
            return BadRequest(ApiResponseDto<List<FitSocial.Application.DTOs.Posts.CreatePostMediaDto>>.Fail($"Media upload failed: {ex.Message}"));
        }
    }
}

