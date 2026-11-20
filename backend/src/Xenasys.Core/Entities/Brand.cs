using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Tenant-scoped brand master (BRD §4.4).
// WHY: Unique NameNormalized per company; soft-delete via IsActive.
// EXAMPLE: CompanyId=5, Name="Acme", Description="House brand".
// ==========================================================================================
public class Brand : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Trim + lower — unique with CompanyId for case-insensitive name.</summary>
    public string NameNormalized { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }
}
