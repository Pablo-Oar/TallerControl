namespace TallerControl.Api.IntegrationTests.Errors;

/// <summary>El sumidero de logs es estático: las pruebas que lo usan no corren en paralelo.</summary>
[CollectionDefinition(Name)]
public sealed class LogCaptureCollection
{
    public const string Name = "log-capture";
}
