namespace Xenasys.Core.Services;

// ==========================================================================================
// WHAT: Builds CHAR(16) product_code = [TenantCode 6] + [UserNn 2] + [Sequence 8].
// WHY: ShortName/CompanyNo removed; TenantCode is the sole company identity segment.
// EXAMPLE: Generate("ACM007", "01", 42) → "ACM0070100000042".
// ==========================================================================================
public static class ProductCodeGenerator
{
    public const int TenantCodeLength = 6;
    public const int UserNnLength = 2;
    public const int SequenceDigits = 8;
    public const int TotalLength = TenantCodeLength + UserNnLength + SequenceDigits;

    /// <summary>
    /// Formats and validates a 16-character product code from TenantCode + creator + seq.
    /// </summary>
    public static string Generate(string tenantCode, string userNn, long sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(userNn);

        var normalizedTenant = tenantCode.Trim().ToUpperInvariant();
        if (normalizedTenant.Length != TenantCodeLength)
        {
            throw new ArgumentException(
                $"TenantCode must be exactly {TenantCodeLength} characters.",
                nameof(tenantCode));
        }

        if (userNn.Length != UserNnLength)
        {
            throw new ArgumentException(
                $"UserNn must be exactly {UserNnLength} characters.",
                nameof(userNn));
        }

        if (sequence < 0 || sequence > 99_999_999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"Sequence must fit in {SequenceDigits} digits (0–99999999).");
        }

        var code =
            $"{normalizedTenant}{userNn}{sequence.ToString($"D{SequenceDigits}")}";
        if (code.Length != TotalLength)
        {
            throw new InvalidOperationException(
                $"Generated product_code length {code.Length} != {TotalLength}.");
        }

        return code;
    }
}
