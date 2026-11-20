namespace Xenasys.Core.Contracts.PlatformAudit;

// ==========================================================================================
// WHAT: Super Admin central audit list DTO (T1.58).
// WHY: GET /api/platform-audit-logs for ops trail without joining every business table.
// EXAMPLE: { action: "Update", entityType: "Tenant", summary: "Update Tenant #2053 TGL" }.
// ==========================================================================================
public sealed class PlatformAuditLogDto
{
    public long Id { get; set; }

    public int ActorUserId { get; set; }

    public string ActorUserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public int? CompanyId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime InsertDate { get; set; }
}
