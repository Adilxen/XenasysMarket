using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

/// <summary>
/// Per-company monotonic counter for CHAR(16) product_code sequence segment.
/// </summary>
public class ProductCodeSequence : ITenantScoped
{
    public int CompanyId { get; set; }

    public long LastValue { get; set; }

    public Tenant? Company { get; set; }
}
