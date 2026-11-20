using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Platform system unit-of-measure master (not tenant-scoped).
// WHY: BRD §4.1 / M2.6 — shared Pcs/Kg/Ltr/Box/Pack seed for Product UoM dropdowns.
// EXAMPLE: Code="Pcs", Name="Pieces", IsActive=true.
// ==========================================================================================
public class Unit : IActivatable
{
    public int Id { get; set; }

    /// <summary>Stable short code — e.g. Pcs, Kg, Ltr.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
