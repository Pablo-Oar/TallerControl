using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TallerControl.Application.Common;

namespace TallerControl.Infrastructure.Storage;

/// <summary>
/// <see cref="IObjectStorage"/> sobre el protocolo S3 (Cloudflare R2 en producción, RustFS en
/// local). Las URLs prefirmadas evitan que la API transfiera el archivo; el servidor solo calcula
/// la clave y firma la URL (doc 03 §flujo de adjuntos, doc 07 §5).
/// </summary>
internal sealed class S3ObjectStorage : IObjectStorage
{
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signer;
    private readonly string _bucket;
    private readonly Protocol _protocol;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        var o = options.Value;
        _bucket = o.Bucket;

        // GetPreSignedURL firma con HTTPS por defecto aunque el ServiceURL sea http (RustFS local): el navegador
        // fallaba con ERR_SSL_PROTOCOL_ERROR al subir fotos y el logo. El protocolo sale del ServiceUrl.
        var signingUrl = string.IsNullOrWhiteSpace(o.PublicUrl) ? o.ServiceUrl : o.PublicUrl.TrimEnd('/');
        _protocol = signingUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;

        _client = new AmazonS3Client(
            new BasicAWSCredentials(o.AccessKey, o.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = o.ServiceUrl,
                ForcePathStyle = o.ForcePathStyle,
                AuthenticationRegion = o.Region,
            });

        // Firmar es un cálculo local: un cliente aparte con la URL pública produce enlaces que el navegador alcanza
        // (la firma incluye el host), sin tocar cómo la API se conecta al almacenamiento.
        _signer = signingUrl == o.ServiceUrl
            ? _client
            : new AmazonS3Client(
                new BasicAWSCredentials(o.AccessKey, o.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = signingUrl,
                    ForcePathStyle = o.ForcePathStyle,
                    AuthenticationRegion = o.Region,
                });
    }

    public string CreatePresignedPutUrl(string fileKey, string contentType, TimeSpan expiry) =>
        _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = fileKey,
            Verb = HttpVerb.PUT,
            Protocol = _protocol,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiry),
        });

    public string CreatePresignedGetUrl(string fileKey, TimeSpan expiry) =>
        _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = fileKey,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = DateTime.UtcNow.Add(expiry),
        });

    public async Task<StoredObject?> HeadAsync(string fileKey, CancellationToken ct)
    {
        try
        {
            var response = await _client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = _bucket, Key = fileKey },
                ct);

            return new StoredObject(response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadPrefixAsync(string fileKey, int byteCount, CancellationToken ct)
    {
        using var response = await _client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = _bucket,
                Key = fileKey,
                ByteRange = new ByteRange(0, byteCount - 1),
            },
            ct);

        using var stream = response.ResponseStream;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
