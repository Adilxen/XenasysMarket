namespace Xenasys.Core.Entities.Common;

/// <summary>
/// Marker for entities that MUST be isolated by company/tenant boundary.
/// </summary>
public interface ITenantScoped
{
    int CompanyId { get; set; }
}
