namespace Xenasys.Core.Entities.Common;

/// <summary>
/// BRD §2 — Audit Metadata Enforcement (also referenced as BaseAuditableEntity).
/// All tables must record insert/update timestamps and user ids.
/// </summary>
public abstract class BaseAuditableEntity
{
    public DateTime InsertDate { get; set; }

    public int InsertUserId { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int? UpdateUserId { get; set; }
}

/// <summary>Backward-compatible alias for <see cref="BaseAuditableEntity"/>.</summary>
public abstract class AuditableEntity : BaseAuditableEntity;
