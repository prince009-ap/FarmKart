using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Profile;

public interface IProfileImageService
{
    Task<string> UploadProfileImageAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        string? oldImageUrl = null,
        CancellationToken cancellationToken = default);

    void DeleteProfileImage(string? imageUrl);
}
