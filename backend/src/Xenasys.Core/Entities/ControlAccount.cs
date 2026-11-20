using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Tenant-scoped COA control node under a platform Master (Inventory, AP, or OTHER).
// WHY: BRD §3.2 — each company owns Control Accounts; INV/AP unique per company for automation.
// EXAMPLE: CompanyId=12, Code="AST-CTL-INV", ControlKind="INVENTORY".
// ==========================================================================================
public class ControlAccount : AuditableEntity, ITenantScoped, IActivatable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int MasterAccountId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>INVENTORY | ACCOUNTS_PAYABLE | OTHER</summary>
    public string ControlKind { get; set; } = ControlAccountKinds.Other;

    public bool IsActive { get; set; } = true;

    public Tenant? Company { get; set; }

    public MasterAccount? MasterAccount { get; set; }

    public ICollection<SubsidiaryAccount> SubsidiaryAccounts { get; set; } = new List<SubsidiaryAccount>();
}
