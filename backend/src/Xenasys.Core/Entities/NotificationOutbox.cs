using Xenasys.Core.Entities.Common;

namespace Xenasys.Core.Entities;

// ==========================================================================================
// WHAT: Async notification outbox (low-stock and future event types).
// WHY: M5.2 / X.2 — never send email/sync side-effects on the HTTP request thread.
// EXAMPLE: EventType=LOW_STOCK, Status=Pending, PayloadJson includes product + qty.
// ==========================================================================================
public class NotificationOutbox : AuditableEntity, ITenantScoped
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public int? ProductId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    /// <summary>Pending | Processed | Failed</summary>
    public string Status { get; set; } = NotificationOutboxStatuses.Pending;

    public DateTime? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public Tenant? Company { get; set; }

    public Product? Product { get; set; }
}

public static class NotificationOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
}

public static class NotificationEventTypes
{
    public const string LowStock = "LOW_STOCK";
}
