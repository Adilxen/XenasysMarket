using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Commercial tenant (Company) entity on the multi-tenant marketplace.
// WHY: Memberships and JWT tenant_id bind to this Id; TenantCode is the unique alternate key.
// EXAMPLE: Id=5, TenantCode="ACME01", IsActive=true.
// ==========================================================================================
public class Tenant : BaseAuditableEntity, IActivatable
{
    public int Id { get; set; }

    /// <summary>Legal / display name of the tenant company.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Fixed CHAR(6) alternate key — UNIQUE(TenantCode). BRD M0.1.</summary>
    public string TenantCode { get; set; } = string.Empty;

    /// <summary>Soft enable/disable — inactive tenants cannot log in or be selected.</summary>
    public bool IsActive { get; set; } = true;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>Optional business address for tenant company profile (Settings).</summary>
    public string? Address { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();

    public ICollection<Brand> Brands { get; set; } = new List<Brand>();

    public ICollection<SubsidiaryAccount> SubsidiaryAccounts { get; set; } = new List<SubsidiaryAccount>();

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}
