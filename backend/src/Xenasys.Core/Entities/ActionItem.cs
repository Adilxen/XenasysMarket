using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Global Actions catalog — verb names used by RBAC (Company-module twin).
// WHY: newSalon Actions master; claim wiring deferred (T1.26 — CRUD first).
// EXAMPLE: Name="Create", IsActive=true.
// ==========================================================================================
public class ActionItem : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
