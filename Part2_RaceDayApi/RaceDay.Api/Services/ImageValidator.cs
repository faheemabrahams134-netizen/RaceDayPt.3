namespace RaceDay.Api.Services;

public record ImageCheck(string? Error, string ContentType, string Extension);

/// <summary>Validates uploaded images by size and by their real file signature (not the client-supplied type).</summary>
public static class ImageValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static async Task<ImageCheck> InspectAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return Fail("Please choose an image file to upload.");
        if (file.Length > MaxBytes) return Fail("The image must be 5 MB or smaller.");

        var header = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
            read = await stream.ReadAtLeastAsync(header.AsMemory(), 12, throwOnEndOfStream: false);

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return new ImageCheck(null, "image/jpeg", ".jpg");
        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return new ImageCheck(null, "image/png", ".png");
        if (read >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return new ImageCheck(null, "image/gif", ".gif");
        if (read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
                        && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP")
            return new ImageCheck(null, "image/webp", ".webp");

        return Fail("Only JPG, PNG, GIF or WebP images are allowed.");
    }

    private static ImageCheck Fail(string message) => new(message, "", "");
}
