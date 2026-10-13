namespace TallerControl.Domain.Common;

/// <summary>Base de las entidades persistidas. La PK es un UUIDv7 generado en la aplicación.</summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
