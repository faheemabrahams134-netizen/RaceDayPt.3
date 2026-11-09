using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>Test double for Azure Blob Storage.</summary>
public class InMemoryBlobStorage : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

    public int Count => _blobs.Count;

    public async Task UploadAsync(string container, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _blobs[$"{container}/{blobName}"] = (ms.ToArray(), contentType);
    }

    public Task<BlobDownload?> DownloadAsync(string container, string blobName, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue($"{container}/{blobName}", out var b)
            ? new BlobDownload(new MemoryStream(b.Data), b.ContentType)
            : null);

    public Task DeleteAsync(string container, string blobName, CancellationToken ct = default)
    {
        _blobs.TryRemove($"{container}/{blobName}", out _);
        return Task.CompletedTask;
    }
}
