using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Form × Action catalog link (newSalon WebFormsAction).
// WHY: Declares which Actions are valid on a Form before Role grants.
// EXAMPLE: Form=/settings/company + Action=Read → FormAction row.
// ==========================================================================================
public class FormAction : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public int ActionId { get; set; }

    public bool IsActive { get; set; } = true;

    public AppForm? Form { get; set; }

    public ActionItem? Action { get; set; }
}
