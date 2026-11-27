using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.API.Services;

public class CloudinaryImageService : IImageService
{
    private static readonly HashSet<string> AllowedImageExtensions = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly Cloudinary _cloudinary;

    public CloudinaryImageService(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    public async Task<List<string>> UploadImagesFromFilesAsync(IFormFileCollection files, CancellationToken cancellationToken = default)
    {
        var uploadedUrls = new List<string>();

        if (files == null || files.Count == 0)
            return uploadedUrls;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length == 0)
                continue;

            // Validate file type (content-type + extension whitelist)
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(ext) ||
                !AllowedImageExtensions.Contains(ext))
                continue;

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
                continue;

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "fitconnect/reviews",
                Transformation = new Transformation().Width(1000).Height(1000).Crop("limit").Quality("auto:good")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

            if (uploadResult.SecureUrl != null)
            {
                uploadedUrls.Add(uploadResult.SecureUrl.AbsoluteUri);
            }
        }

        return uploadedUrls;
    }

    public async Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new System.Uri(imageUrl);
            var segments = uri.Segments;
            var publicId = string.Join("", segments).Split('.')[0];
            var lastSlash = publicId.LastIndexOf('/');
            if (lastSlash >= 0)
                publicId = publicId.Substring(lastSlash + 1);

            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch
        {
            return false;
        }
    }
}
