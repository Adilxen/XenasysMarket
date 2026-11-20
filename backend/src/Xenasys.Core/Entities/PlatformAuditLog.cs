namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Central Super Admin operations trail (BRD §3.1 / T1.58) — not row-level Insert/Update stamps.
// WHY: One queryable log of platform mutations across Tenants/Users/Roles/Forms/Actions.
// EXAMPLE: ActorUserId=1, Action=Create, EntityType=Tenant, EntityId=2056, Summary="Create Tenant #2056 TGL".
// ==========================================================================================
public sealed class PlatformAuditLog
{
    public long Id { get; set; }

    public int ActorUserId { get; set; }

    public string ActorUserName { get; set; } = string.Empty;

    /// <summary>Create | Update | Delete | Login</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public int? CompanyId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? PayloadJson { get; set; }

    public DateTime InsertDate { get; set; }
}
