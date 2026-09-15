using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FitSocial.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10MB (coach credentials)

    private readonly IConfiguration _configuration;

    public UploadController(IConfiguration configuration)
    {
        _configuration = configuration;
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
            var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));

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
}
