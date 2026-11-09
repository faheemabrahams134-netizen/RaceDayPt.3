using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Concurrent;

namespace RaceDay.Api.Services;

public static class BlobContainers
{
    public const string EventBanners = "event-banners";
    public const string ProfilePictures = "profile-pictures";
}

public record BlobDownload(Stream Content, string ContentType);

/// <summary>All file storage goes through this abstraction so the MVC app never talks to Azure directly.</summary>
public interface IBlobStorageService
{
    Task UploadAsync(string container, string blobName, Stream content, string contentType, CancellationToken ct = default);
    Task<BlobDownload?> DownloadAsync(string container, string blobName, CancellationToken ct = default);
    Task DeleteAsync(string container, string blobName, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage implementation (also works against the Azurite emulator). Containers are private.</summary>
public class AzureBlobStorageService(BlobServiceClient client) : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, bool> _created = new();

    private async Task<BlobContainerClient> ContainerAsync(string name, CancellationToken ct)
    {
        var container = client.GetBlobContainerClient(name);
        if (_created.TryAdd(name, true))
        {
            try
            {
                await container.CreateIfNotExistsAsync(cancellationToken: ct);
            }
            catch
            {
                _created.TryRemove(name, out _);
                throw;
            }
        }
        return container;
    }

    public async Task UploadAsync(string container, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var blob = (await ContainerAsync(container, ct)).GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    public async Task<BlobDownload?> DownloadAsync(string container, string blobName, CancellationToken ct = default)
    {
        var blob = (await ContainerAsync(container, ct)).GetBlobClient(blobName);
        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return new BlobDownload(response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string container, string blobName, CancellationToken ct = default)
    {
        var blob = (await ContainerAsync(container, ct)).GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
