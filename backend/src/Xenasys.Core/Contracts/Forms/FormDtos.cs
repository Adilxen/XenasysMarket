using System.ComponentModel.DataAnnotations;

namespace Xenasys.Core.Contracts.Forms;

// ==========================================================================================
// WHAT: Wire DTOs for Forms module (Name, Path, IsActive).
// WHY: Company-pattern list/detail — Path unique among active rows.
// EXAMPLE: CreateFormRequest { name, path }.
// ==========================================================================================

public sealed class FormDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class CreateFormRequest
{
    [Required, MinLength(2), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(200)]
    public string Path { get; set; } = string.Empty;
}

public sealed class UpdateFormRequest
{
    [Required, MinLength(2), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(200)]
    public string Path { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
