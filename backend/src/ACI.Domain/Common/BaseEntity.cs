namespace ACI.Domain.Common;

/// <summary>
/// Base for entities with a GUID primary key.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
}
