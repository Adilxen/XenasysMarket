using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Platform-shared COA master root (Assets, Equity, Liabilities, Income, Expense).
// WHY: BRD §4.7.1 — tenants read; Super Admin writes; ControlAccounts hang off these nodes.
// EXAMPLE: Code="1000", Title="Assets", Prefix="AST".
// ==========================================================================================
public class MasterAccount : AuditableEntity, IActivatable
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ControlAccount> ControlAccounts { get; set; } = new List<ControlAccount>();
}
