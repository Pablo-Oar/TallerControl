using Microsoft.Extensions.Options;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Storage;
using Xunit;

namespace TallerControl.Api.IntegrationTests.Storage;

/// <summary>
/// Las URLs prefirmadas deben respetar el esquema del ServiceUrl: el SDK firma con HTTPS por defecto y el RustFS
/// local es http, lo que rompía la subida directa desde el navegador (fotos, logo).
/// </summary>
public sealed class S3PresignedUrlTests
{
    private static IObjectStorage Create(string serviceUrl)
    {
        var options = Options.Create(new StorageOptions
        {
            ServiceUrl = serviceUrl,
            AccessKey = "test-access",
            SecretKey = "test-secret",
            Bucket = "bucket",
        });
        var type = typeof(StorageOptions).Assembly.GetType("TallerControl.Infrastructure.Storage.S3ObjectStorage")!;
        return (IObjectStorage)Activator.CreateInstance(type, options)!;
    }

    [Theory(DisplayName = "Storage: la URL prefirmada usa el esquema del ServiceUrl")]
    [InlineData("http://127.0.0.1:9000", "http://")]
    [InlineData("https://acct.r2.cloudflarestorage.com", "https://")]
    public void Presigned_urls_follow_the_service_url_scheme(string serviceUrl, string expectedScheme)
    {
        var storage = Create(serviceUrl);

        Assert.StartsWith(expectedScheme, storage.CreatePresignedPutUrl("a/b.jpg", "image/jpeg", TimeSpan.FromMinutes(5)));
        Assert.StartsWith(expectedScheme, storage.CreatePresignedGetUrl("a/b.jpg", TimeSpan.FromMinutes(10)));
    }
}
