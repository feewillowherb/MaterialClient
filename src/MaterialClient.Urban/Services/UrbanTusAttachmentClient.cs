using System.Net.Http.Headers;
using System.Text;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Thin tus 1.0 client for UrbanManagement attachment uploads.
/// </summary>
public interface IUrbanTusAttachmentClient : ITransientDependency
{
    /// <summary>
    ///     Upload a local file via tus and return the server tus file id.
    /// </summary>
    Task<string> UploadFileAsync(
        string absolutePath,
        string buildLicenseNo,
        AttachType attachType,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
[AutoConstructor]
public partial class UrbanTusAttachmentClient : IUrbanTusAttachmentClient
{
    public const string HttpClientName = "UrbanManagementTus";
    public const int DefaultChunkSizeBytes = 256 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UrbanTusAttachmentClient> _logger;

    public async Task<string> UploadFileAsync(
        string absolutePath,
        string buildLicenseNo,
        AttachType attachType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildLicenseNo);

        if (attachType is not (AttachType.Lpr or AttachType.UrbanPhoto))
        {
            throw new ArgumentOutOfRangeException(nameof(attachType), attachType, "Must be Lrp or UrbanPhoto");
        }

        var fileInfo = new FileInfo(absolutePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Attachment file not found", absolutePath);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var metadata = EncodeMetadata(new Dictionary<string, string>
        {
            ["filename"] = fileInfo.Name,
            ["filetype"] = "image/jpeg",
            ["buildlicenseno"] = buildLicenseNo.Trim(),
            ["attachtype"] = ((short)attachType).ToString()
        });

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "api/urban-attachment/tus/");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", fileInfo.Length.ToString());
        createRequest.Headers.Add("Upload-Metadata", metadata);
        createRequest.Content = new ByteArrayContent([]);
        createRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");

        using var createResponse = await client.SendAsync(createRequest, cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            var body = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Tus create failed: {(int)createResponse.StatusCode} {createResponse.ReasonPhrase}. {body}");
        }

        if (createResponse.Headers.Location is null)
        {
            throw new HttpRequestException("Tus create response missing Location header");
        }

        var fileUrl = createResponse.Headers.Location.IsAbsoluteUri
            ? createResponse.Headers.Location
            : new Uri(client.BaseAddress!, createResponse.Headers.Location);

        var fileId = fileUrl.Segments[^1].TrimEnd('/');
        _logger.LogInformation(
            "Tus upload created: FileId={FileId}, Size={Size}, AttachType={AttachType}",
            fileId,
            fileInfo.Length,
            attachType);

        await using var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: DefaultChunkSizeBytes,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        long offset = 0;
        var buffer = new byte[DefaultChunkSizeBytes];
        while (offset < fileInfo.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, fileUrl);
            patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
            patchRequest.Headers.Add("Upload-Offset", offset.ToString());
            var chunk = new byte[read];
            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
            patchRequest.Content = new ByteArrayContent(chunk);
            patchRequest.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/offset+octet-stream");
            patchRequest.Content.Headers.ContentLength = read;

            using var patchResponse = await client.SendAsync(patchRequest, cancellationToken);
            if (!patchResponse.IsSuccessStatusCode)
            {
                var body = await patchResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Tus patch failed at offset {offset}: {(int)patchResponse.StatusCode}. {body}");
            }

            if (patchResponse.Headers.TryGetValues("Upload-Offset", out var offsetValues) &&
                long.TryParse(offsetValues.FirstOrDefault(), out var serverOffset))
            {
                offset = serverOffset;
            }
            else
            {
                offset += read;
            }
        }

        _logger.LogInformation("Tus upload completed: FileId={FileId}", fileId);
        return fileId;
    }

    private static string EncodeMetadata(IReadOnlyDictionary<string, string> pairs)
    {
        return string.Join(",",
            pairs.Select(kv => $"{kv.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value))}"));
    }
}
