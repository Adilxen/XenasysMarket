using System.Text;

namespace Xenasys.Core.Services;

// ==========================================================================================
// WHAT: Builds CHAR(6) TenantCode = [NamePrefix 2] + [Sequence 4] (concatenation).
// WHY: Production marketplace scale — up to 9,999 tenants; unique via atomic sequence.
// EXAMPLE: name "Unilever Pakistan", seq 53 → "UN0053".
// ==========================================================================================
public static class TenantCodeGenerator
{
    public const int PrefixLength = 2;
    public const int SequenceDigits = 4;
    public const int TotalLength = PrefixLength + SequenceDigits;
    public const long MaxSequence = 9_999;

    public const string SequenceKey = "TENANT_CODE";

    /// <summary>
    /// Concatenates a 2-char name prefix with a zero-padded sequence (CHAR(6)).
    /// </summary>
    public static string Generate(string companyName, long sequence)
    {
        if (sequence < 1 || sequence > MaxSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"Tenant sequence must be 1–{MaxSequence} for CHAR(6) codes.");
        }

        var prefix = BuildPrefix(companyName);
        var code = $"{prefix}{sequence.ToString($"D{SequenceDigits}")}";
        if (code.Length != TotalLength)
        {
            throw new InvalidOperationException(
                $"Generated TenantCode length {code.Length} != {TotalLength}.");
        }

        return code;
    }

    /// <summary>First 2 A–Z / 0–9 chars from the company name; pads with X.</summary>
    public static string BuildPrefix(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return new string('X', PrefixLength);
        }

        var sb = new StringBuilder(PrefixLength);
        foreach (var ch in companyName.ToUpperInvariant())
        {
            if (ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                sb.Append(ch);
                if (sb.Length == PrefixLength)
                {
                    break;
                }
            }
        }

        while (sb.Length < PrefixLength)
        {
            sb.Append('X');
        }

        return sb.ToString();
    }
}
