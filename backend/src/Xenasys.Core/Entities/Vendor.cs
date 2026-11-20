using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

/// <summary>
/// BRD §4.6 — Tenant-scoped vendor master with linked AP subsidiary ledger.
/// </summary>
public class Vendor : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public string VendorName { get; set; } = string.Empty;

    /// <summary>Optional NTN — documents mask 1234567-8 when provided; unique per company.</summary>
    public string? NtnNo { get; set; }

    public string Address { get; set; } = string.Empty;

    public string VendorPhone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string RepresentativeName { get; set; } = string.Empty;

    public string RepresentativeNo { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>System AP subsidiary created on vendor save.</summary>
    public int? SubsidiaryAccountId { get; set; }

    public Tenant? Company { get; set; }

    public SubsidiaryAccount? SubsidiaryAccount { get; set; }
}
