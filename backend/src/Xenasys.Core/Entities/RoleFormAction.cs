using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Role × Form × Action grant (newSalon RoleWebFormsAction) — company via Role.CompanyId.
// WHY: SSoT for screen access; PermissionGate derives module.action codes from Path+Action.
// EXAMPLE: Role ME0002 + Form=/settings/users + Action=Read.
// ==========================================================================================
public class RoleFormAction : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public int FormId { get; set; }

    public int ActionId { get; set; }

    public bool IsActive { get; set; } = true;

    public Role? Role { get; set; }

    public AppForm? Form { get; set; }

    public ActionItem? Action { get; set; }
}
