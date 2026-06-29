using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace iM1os.Web.Services;

public sealed record StoredDocumentUpload(string FileName, string Url, string ContentType, string StorageKey);

public static class DocumentUploadStorage
{
    private const long MaxFileBytes = 50 * 1024 * 1024;

    public static async Task<IReadOnlyCollection<StoredDocumentUpload>> SaveAsync(
        IWebHostEnvironment environment,
        Guid organizationId,
        string area,
        Guid recordId,
        IReadOnlyCollection<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            return [];
        }

        var uploads = new List<StoredDocumentUpload>();
        var relativeDirectory = Path.Combine(organizationId.ToString("N"), area, recordId.ToString("N"), "documents");
        var physicalDirectory = Path.Combine(UploadRoot(environment), relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        foreach (var file in files.Where(x => x.Length > 0))
        {
            if (file.Length > MaxFileBytes)
            {
                throw new InvalidOperationException("Document uploads are limited to 50 MB per file.");
            }

            var safeName = SafeFileName(file.FileName);
            var storageName = $"{Guid.NewGuid():N}-{safeName}";
            var physicalPath = Path.Combine(physicalDirectory, storageName);

            await using (var stream = File.Create(physicalPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var storageKey = Path.Combine(relativeDirectory, storageName).Replace('\\', '/');
            var urlPath = Path.Combine("uploads", storageKey).Replace('\\', '/');
            uploads.Add(new StoredDocumentUpload(
                safeName,
                "/" + urlPath,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                storageKey));
        }

        return uploads;
    }

    public static string UploadRoot(IWebHostEnvironment environment)
    {
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "uploads"));
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "document";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return name.Length <= 180 ? name : name[..180];
    }
}
