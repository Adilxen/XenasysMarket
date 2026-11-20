using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Application Forms catalog — navigable screens for permission-management.
// WHY: Stores Form Name + Path for every route that can be claim-gated later.
// EXAMPLE: Name="Company", Path="/settings/company", IsActive=true.
// ==========================================================================================
public class AppForm : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>App route / navigation path (e.g. /settings/roles).</summary>
    public string Path { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
