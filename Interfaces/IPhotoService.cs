/*using CloudinaryDotNet.Actions;

namespace grad.Interfaces
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> UploadPhotoAsynic(IFormFile file);
        Task<DeletionResult> DeletePhotoAsynic(string PublicId);
    }
}*/
using CloudinaryDotNet.Actions;

namespace grad.Interfaces
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> UploadPhotoAsync(IFormFile file);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
    }
}