using System.ComponentModel.DataAnnotations;

namespace Xenasys.Core.Contracts.Actions;

// ==========================================================================================
// WHAT: Wire DTOs for Actions module (Name, IsActive).
// WHY: Company-pattern list/detail — Name unique among active rows.
// EXAMPLE: CreateActionRequest { name }.
// ==========================================================================================

public sealed class ActionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class CreateActionRequest
{
    [Required, MinLength(2), MaxLength(50)]
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateActionRequest
{
    [Required, MinLength(2), MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
