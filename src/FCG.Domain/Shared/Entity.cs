namespace FCG.Domain.Shared;

/// <summary>
/// Classe base para todas as entidades de domínio. Fornece identidade via <see cref="Id"/> e timestamps de auditoria.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}
