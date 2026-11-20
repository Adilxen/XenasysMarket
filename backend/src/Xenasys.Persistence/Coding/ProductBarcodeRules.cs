using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Entities;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Product barcode normalize / validate / tenant-unique / auto-generate when empty.
// WHY: BRD §4.5 — optional; alnum+hyphen; commas for multi; unique within tenant (T3.9).
// EXAMPLE: empty → ProductCode; "ABC-1,XYZ-2" → tokens unique per company.
// ==========================================================================================
public static class ProductBarcodeRules
{
    private static readonly Regex Allowed = new(
        @"^[A-Za-z0-9,-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Token = new(
        @"^[A-Za-z0-9-]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Resolves barcode for save: empty → auto ProductCode; else normalize + uniqueness check.
    /// Returns error message or null when OK (resolved out).
    /// </summary>
    public static async Task<(string? Barcode, string? Error)> ResolveAsync(
        ApplicationDbContext db,
        int companyId,
        string? rawBarcode,
        string productCode,
        int? excludeProductId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (rawBarcode ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            // Auto-generate from immutable ProductCode (already unique per company).
            var auto = SanitizeToken(productCode);
            if (auto.Length == 0)
            {
                return (null, "Unable to auto-generate barcode from Product Code.");
            }

            var autoClash = await TokenTakenAsync(
                db,
                companyId,
                auto,
                excludeProductId,
                cancellationToken);
            if (autoClash)
            {
                // Extremely rare: another product already stored this code as a barcode token.
                auto = $"BC-{auto}";
                if (auto.Length > 500)
                {
                    auto = auto[..500];
                }

                autoClash = await TokenTakenAsync(
                    db,
                    companyId,
                    auto,
                    excludeProductId,
                    cancellationToken);
                if (autoClash)
                {
                    return (null, "Auto-generated barcode collides with an existing product. Enter a barcode manually.");
                }
            }

            return (auto, null);
        }

        if (trimmed.Length > 500)
        {
            return (null, "Barcode must be at most 500 characters.");
        }

        if (!Allowed.IsMatch(trimmed))
        {
            return (null, "Barcode may only contain letters, numbers, commas, and hyphens.");
        }

        var tokens = SplitTokens(trimmed);
        if (tokens.Count == 0)
        {
            return (null, "Barcode may only contain letters, numbers, commas, and hyphens.");
        }

        if (tokens.Count != tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return (null, "Barcode list contains duplicate values.");
        }

        foreach (var token in tokens)
        {
            if (!Token.IsMatch(token))
            {
                return (null, "Barcode may only contain letters, numbers, commas, and hyphens.");
            }

            if (await TokenTakenAsync(db, companyId, token, excludeProductId, cancellationToken))
            {
                return (null, $"Barcode '{token}' is already used by another product in this company.");
            }
        }

        // Canonical storage: trim tokens, join with comma (no spaces).
        return (string.Join(',', tokens), null);
    }

    public static IReadOnlyList<string> SplitTokens(string barcode) =>
        barcode
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();

    private static string SanitizeToken(string value)
    {
        var chars = value.Trim().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray();
        return new string(chars);
    }

    private static async Task<bool> TokenTakenAsync(
        ApplicationDbContext db,
        int companyId,
        string token,
        int? excludeProductId,
        CancellationToken cancellationToken)
    {
        var query = db.Products
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.Barcode != null && p.Barcode != "");

        if (excludeProductId is int skip)
        {
            query = query.Where(p => p.Id != skip);
        }

        // Load candidate barcode strings for the company (bounded by tenant product count).
        var barcodes = await query.Select(p => p.Barcode!).ToListAsync(cancellationToken);
        foreach (var stored in barcodes)
        {
            foreach (var existing in SplitTokens(stored))
            {
                if (string.Equals(existing, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
