namespace Xenasys.Core.Time;

// ==========================================================================================
// WHAT: Pakistan business-clock for InsertDate / UpdateDate audit stamps.
// WHY: Operators read audit times in PKT (UTC+5); DateTime.UtcNow looked "5 hours wrong" in SSMS.
// EXAMPLE: AuditClock.Now → 2026-09-11 22:06 when UTC is 17:06.
// ==========================================================================================
public static class AuditClock
{
    private static readonly TimeZoneInfo Pakistan = ResolvePakistan();

    /// <summary>Current instant in Pakistan Standard Time (Unspecified kind for datetime2).</summary>
    public static DateTime Now
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Pakistan);
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }

    private static TimeZoneInfo ResolvePakistan()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Pakistan Standard Time" : "Asia/Karachi");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "PKT",
                TimeSpan.FromHours(5),
                "Pakistan Standard Time",
                "Pakistan Standard Time");
        }
    }
}
