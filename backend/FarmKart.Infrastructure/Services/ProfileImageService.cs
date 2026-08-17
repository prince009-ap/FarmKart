using FarmKart.Application.Abstractions.Profile;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class ProfileImageService : IProfileImageService
{
    private readonly IWebHostEnvironment _environment;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/webp", "image/pjpeg", "image/x-png"];

    public ProfileImageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> UploadProfileImageAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        string? oldImageUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileLength <= 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        if (fileLength > MaxFileSizeBytes)
        {
            throw new ArgumentException("Image size must be less than 5 MB.");
        }

        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Image must be JPG, PNG or WEBP.");
        }

        if (!string.IsNullOrEmpty(contentType) && !AllowedMimeTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new ArgumentException("Image must be JPG, PNG or WEBP.");
        }

        // Magic Header Inspection
        byte[] header = new byte[8];
        var bytesRead = await fileStream.ReadAsync(header, 0, header.Length, cancellationToken);
        fileStream.Position = 0; // reset stream position

        if (bytesRead < 4 || !IsValidImageHeader(header))
        {
            throw new ArgumentException("Image must be JPG, PNG or WEBP.");
        }

        var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        var uploadsFolder = Path.Combine(webRoot, "uploads", "profile-images");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{userId}-{Guid.NewGuid()}{ext}";
        var newFilePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var outputStream = new FileStream(newFilePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream, cancellationToken);
        }

        var relativeUrl = $"/uploads/profile-images/{uniqueFileName}";

        // Safely delete old image if replacement succeeds
        if (!string.IsNullOrWhiteSpace(oldImageUrl))
        {
            DeleteProfileImage(oldImageUrl);
        }

        return relativeUrl;
    }

    public void DeleteProfileImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        try
        {
            var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? _environment.WebRootPath
                : Path.Combine(_environment.ContentRootPath, "wwwroot");

            // Extract relative filename safely
            var normalizedPath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, normalizedPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // Ignore file deletion errors to avoid breaking application workflow
        }
    }

    private static bool IsValidImageHeader(byte[] header)
    {
        // JPEG magic header: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        // PNG magic header: 89 50 4E 47
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;

        // WEBP magic header: 52 49 46 46 (RIFF)
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
            return true;

        return false;
    }
}
