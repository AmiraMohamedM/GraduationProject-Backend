using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using grad.Helpers;
using grad.Interfaces;
using Microsoft.Extensions.Options;
using Npgsql.BackendMessages;
using System.Reflection.Metadata;
using System.Security.Principal;

namespace grad.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;

        public PhotoService(IOptions<CloudinarySettings> config)
        {
            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret);

            _cloudinary = new Cloudinary(account);
        }

        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            var deletionParams = new DeletionParams(publicId);
            return await _cloudinary.DestroyAsync(deletionParams);
        }

        public async Task<ImageUploadResult> UploadPhotoAsync(IFormFile file)
        {
            var uploadResult = new ImageUploadResult();

            if (file != null && file.Length > 0)
            {
                await using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Transformation = new Transformation()
                        .Height(500)
                        .Width(500)
                        .Crop("fill")
                        .Gravity("face"),
                    Folder = "Grad-Photos"
                };

                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            return uploadResult;
        }
    
}
}