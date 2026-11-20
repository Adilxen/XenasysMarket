namespace Xenasys.Core.Services;

// ==========================================================================================
// WHAT: Single SSoT for subsidiary Account Code + Account No system formats.
// WHY: Built-in formats — allocated once at create; never updated afterward.
// EXAMPLE: Seq 42 → AccountNo "ACC-2026-00042", AccountCode "ACM007-M00042".
// ==========================================================================================
public static class SubsidiaryAccountCoding
{
    public const int TenantCodeLength = 6;
    public const int AccountNoSequenceDigits = 5;
    public const int AccountNoMaxLength = 20;
    public const int ManualCodeSequenceDigits = 5;
    public const string AccountNoPrefix = "ACC";

    public const string KindManual = "M";
    public const string KindProduct = "P";
    public const string KindVendor = "V";
    public const string BaselineCash = "CASH";
    public const string BaselineBank = "BANK";

    /// <summary>BRD Account No = ACC-{yyyy}-{seq5}. Immutable after create.</summary>
    public static string FormatAccountNo(int year, long sequence)
    {
        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be four digits.");
        }

        EnsureSequence(sequence, AccountNoSequenceDigits);
        return $"{AccountNoPrefix}-{year}-{sequence.ToString($"D{AccountNoSequenceDigits}")}";
    }

    /// <summary>
    /// Account Code for sequenced kinds: {TenantCode}-{M|P|V}{seq5}. Immutable after create.
    /// </summary>
    public static string FormatSequencedCode(string tenantCode, string kind, long sequence)
    {
        var tenant = NormalizeTenantCode(tenantCode);
        var k = (kind ?? string.Empty).Trim().ToUpperInvariant();
        if (k is not (KindManual or KindProduct or KindVendor))
        {
            throw new ArgumentException(
                "Kind must be M (manual), P (product), or V (vendor).",
                nameof(kind));
        }

        EnsureSequence(sequence, ManualCodeSequenceDigits);
        return $"{tenant}-{k}{sequence.ToString($"D{ManualCodeSequenceDigits}")}";
    }

    /// <summary>Baseline Account Code = {TenantCode}-CASH | {TenantCode}-BANK.</summary>
    public static string FormatBaselineCode(string tenantCode, string baselineSuffix)
    {
        var tenant = NormalizeTenantCode(tenantCode);
        var suffix = (baselineSuffix ?? string.Empty).Trim().ToUpperInvariant();
        if (suffix is not (BaselineCash or BaselineBank))
        {
            throw new ArgumentException(
                "Baseline suffix must be CASH or BANK.",
                nameof(baselineSuffix));
        }

        return $"{tenant}-{suffix}";
    }

    /// <summary>True when AccountNo is not ACC-{yyyy}-{seq}.</summary>
    public static bool AccountNoNeedsRewrite(string? accountNo)
    {
        if (string.IsNullOrWhiteSpace(accountNo))
        {
            return false;
        }

        var trimmed = accountNo.Trim();
        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return !(parts.Length == 3
            && string.Equals(parts[0], AccountNoPrefix, StringComparison.OrdinalIgnoreCase)
            && parts[1].Length == 4
            && int.TryParse(parts[1], out _)
            && long.TryParse(parts[2], out _));
    }

    /// <summary>Extracts trailing sequence from BRD or legacy AccountNo variants.</summary>
    public static bool TryParseAccountNoSequence(string accountNo, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(accountNo))
        {
            return false;
        }

        var trimmed = accountNo.Trim();

        // Legacy CHAR(12): TenantCode + seq6
        if (trimmed.Length == 12 && !trimmed.Contains('-'))
        {
            return long.TryParse(trimmed.AsSpan(6, 6), out sequence);
        }

        // Legacy CHAR(16): TenantCode + yyyy + seq6
        if (trimmed.Length == 16 && !trimmed.Contains('-'))
        {
            return long.TryParse(trimmed.AsSpan(10, 6), out sequence);
        }

        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[^1], out sequence);
    }

    /// <summary>Extracts year from BRD AccountNo or returns false.</summary>
    public static bool TryParseAccountNoYear(string accountNo, out int year)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(accountNo))
        {
            return false;
        }

        var parts = accountNo.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3
            && string.Equals(parts[0], AccountNoPrefix, StringComparison.OrdinalIgnoreCase)
            && parts[1].Length == 4)
        {
            return int.TryParse(parts[1], out year);
        }

        // Legacy CHAR(16): positions 6..9 are yyyy
        var trimmed = accountNo.Trim();
        if (trimmed.Length == 16 && !trimmed.Contains('-'))
        {
            return int.TryParse(trimmed.AsSpan(6, 4), out year);
        }

        return false;
    }

    private static string NormalizeTenantCode(string tenantCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantCode);
        var normalized = tenantCode.Trim().ToUpperInvariant();
        if (normalized.Length != TenantCodeLength)
        {
            throw new ArgumentException(
                $"TenantCode must be exactly {TenantCodeLength} characters.",
                nameof(tenantCode));
        }

        return normalized;
    }

    private static void EnsureSequence(long sequence, int digits)
    {
        var max = (long)Math.Pow(10, digits) - 1;
        if (sequence < 0 || sequence > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"Sequence must fit in {digits} digits (0–{max}).");
        }
    }
}
