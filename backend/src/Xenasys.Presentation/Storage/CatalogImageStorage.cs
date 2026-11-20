using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Xenasys.Presentation.Storage;

// ==========================================================================================
// WHAT: Saves catalog images under wwwroot/uploads/{companyId}/{folder}/.
// WHY: BRD §4.2–4.3 — JPG/JPEG/PNG ≤ 2MB; GUID filename; relative path for ImagePath.
// EXAMPLE: SaveAsync(file, 5, "categories") → "/uploads/5/categories/{guid}.jpg".
// ==========================================================================================
public sealed class CatalogImageStorage
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
    };

    private readonly IWebHostEnvironment _env;

    public CatalogImageStorage(IWebHostEnvironment env)
    {
        _env = env;
    }

    public static string? Validate(IFormFile? file, bool required)
    {
        if (file is null || file.Length == 0)
        {
            return required ? "Image is required (JPG/JPEG/PNG, max 2MB)." : null;
        }

        if (file.Length > MaxBytes)
        {
            return "Image must be at most 2MB.";
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return "Image must be JPG, JPEG, or PNG.";
        }

        var contentType = (file.ContentType ?? string.Empty).Trim();
        if (contentType.Length > 0 && !AllowedContentTypes.Contains(contentType))
        {
            return "Image content-type must be image/jpeg or image/png.";
        }

        return null;
    }

    public async Task<string> SaveAsync(
        IFormFile file,
        int companyId,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg")
        {
            ext = ".jpg";
        }

        var relativeDir = Path.Combine("uploads", companyId.ToString(), folder);
        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;
        var absoluteDir = Path.Combine(webRoot, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var absolutePath = Path.Combine(absoluteDir, fileName);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        // Web-relative path with forward slashes for static file serving.
        return $"/{relativeDir.Replace('\\', '/')}/{fileName}";
    }
}
