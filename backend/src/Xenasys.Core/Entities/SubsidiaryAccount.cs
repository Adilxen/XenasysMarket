using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

/// <summary>
/// BRD §4.7.3 — Transaction-level sub-ledger under a Control Account.
/// Auto-created for Products (Inventory) and Vendors (AP).
/// </summary>
public class SubsidiaryAccount : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int ControlAccountId { get; set; }

    /// <summary>System Account Code — e.g. ACM007-CASH, ACM007-M00042. Immutable after create.</summary>
    public string SubsidiaryAccountCode { get; set; } = string.Empty;

    public string SubsidiaryAccountTitle { get; set; } = string.Empty;

    /// <summary>System Account No — e.g. ACC-2026-00042. Immutable after create.</summary>
    public string AccountNo { get; set; } = string.Empty;

    /// <summary>Optional inventory link — CHAR(16) product_code.</summary>
    public string? ProductCode { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>When true, deletion is blocked (linked product/vendor ledger).</summary>
    public bool IsSystemGenerated { get; set; }

    public string? SourceEntityType { get; set; }

    public int? SourceEntityId { get; set; }

    public Tenant? Company { get; set; }

    public ControlAccount? ControlAccount { get; set; }
}
