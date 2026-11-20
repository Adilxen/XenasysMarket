using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Xenasys.Core.Abstractions;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: Middleware that rejects authenticated requests whose inbound company_id disagrees with JWT tenant_id.
// WHY: company_id is wire-only; isolation SSoT is JWT tenant_id → ITenantProvider (BRD §2 / §3.2 / T0.5 / T1.60).
// EXAMPLE: JWT tenant_id=5 + JSON body company_id=9 → HTTP 403 TENANT_CLAIM_MISMATCH.
// NOTE: Super Admin is skipped — SA create may send company_id as provisioning target only
//       (Users/Roles membership create). That does not weaken tenant query filters for non-SA.
// ==========================================================================================
public sealed class TenantClaimGuardMiddleware
{
    private static readonly HashSet<string> JsonContentHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/*+json",
        "text/json",
    };

    private readonly RequestDelegate _next;

    public TenantClaimGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // ==========================================================================================
    // WHAT: Compares query/route/header/JSON-body company_id to the ambient bound tenant when authenticated.
    // WHY: Blocks tenant-hopping via client-supplied company_id after login (incl. POST/PUT bodies).
    // EXAMPLE: Bound tenant 12, body { "company_id": 12 } → continue; company_id=99 → 403 JSON error.
    // ==========================================================================================
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && !tenantProvider.IsSuperAdmin
            && tenantProvider.TenantId is int bound and > 0
            && await TryReadInboundCompanyIdAsync(context) is { Found: true, CompanyId: var inbound }
            && inbound > 0
            && inbound != bound)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = new
                {
                    code = "TENANT_CLAIM_MISMATCH",
                    message =
                        "Request company_id does not match the authenticated tenant claim.",
                    detail = $"{TenancyKeyNames.CompanyId}={inbound}",
                },
            });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Hot-path: query → route → header → optional JSON body peek for sole key <c>company_id</c>.
    /// </summary>
    private static async Task<(bool Found, int CompanyId)> TryReadInboundCompanyIdAsync(HttpContext context)
    {
        const string key = TenancyKeyNames.CompanyId;

        if (context.Request.Query.TryGetValue(key, out var q)
            && int.TryParse(q.FirstOrDefault(), out var fromQuery))
        {
            return (true, fromQuery);
        }

        if (context.Request.RouteValues.TryGetValue(key, out var rv)
            && rv is not null
            && int.TryParse(Convert.ToString(rv), out var fromRoute))
        {
            return (true, fromRoute);
        }

        if (context.Request.Headers.TryGetValue(key, out var header)
            && int.TryParse(header.FirstOrDefault(), out var fromHeader))
        {
            return (true, fromHeader);
        }

        if (await TryReadCompanyIdFromJsonBodyAsync(context.Request) is int fromBody)
        {
            return (true, fromBody);
        }

        return (false, 0);
    }

    private static async Task<int?> TryReadCompanyIdFromJsonBodyAsync(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsDelete(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || request.ContentLength is 0)
        {
            return null;
        }

        var contentType = request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType)
            || !JsonContentHints.Any(h =>
                contentType.StartsWith(h.Replace("*+json", ""), StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        request.EnableBuffering();
        try
        {
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, TenancyKeyNames.CompanyId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.Number
                    && prop.Value.TryGetInt32(out var n))
                {
                    return n;
                }

                if (prop.Value.ValueKind == JsonValueKind.String
                    && int.TryParse(prop.Value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
            request.Body.Position = 0;
        }

        return null;
    }
}
