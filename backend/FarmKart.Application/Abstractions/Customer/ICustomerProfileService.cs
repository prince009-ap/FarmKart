using FarmKart.Application.DTOs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Customer;

public interface ICustomerProfileService
{
    Task<CustomerProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerProfileResponse> UpdateProfileAsync(Guid userId, UpdateCustomerProfileRequest request, CancellationToken cancellationToken = default);
    Task<CustomerProfileResponse> UploadProfileImageAsync(Guid userId, Stream stream, string fileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task<CustomerProfileResponse> RemoveProfileImageAsync(Guid userId, CancellationToken cancellationToken = default);
}
