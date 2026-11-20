using System.Text;

namespace Xenasys.Core.Services;

// ==========================================================================================
// WHAT: Builds CHAR(6) Role Code = [NamePrefix 2] + [Sequence 4] (concatenation).
// WHY: Same Company-module formula — up to 9,999 custom roles; system roles keep long codes.
// EXAMPLE: name "Inventory Manager", seq 12 → "IN0012".
// ==========================================================================================
public static class RoleCodeGenerator
{
    public const int PrefixLength = 2;
    public const int SequenceDigits = 4;
    public const int TotalLength = PrefixLength + SequenceDigits;
    public const long MaxSequence = 9_999;

    public const string SequenceKey = "ROLE_CODE";

    public static string Generate(string roleName, long sequence)
    {
        if (sequence < 1 || sequence > MaxSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"Role sequence must be 1–{MaxSequence} for CHAR(6) codes.");
        }

        var prefix = BuildPrefix(roleName);
        var code = $"{prefix}{sequence.ToString($"D{SequenceDigits}")}";
        if (code.Length != TotalLength)
        {
            throw new InvalidOperationException(
                $"Generated Role Code length {code.Length} != {TotalLength}.");
        }

        return code;
    }

    public static string BuildPrefix(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return new string('X', PrefixLength);
        }

        var sb = new StringBuilder(PrefixLength);
        foreach (var ch in roleName.ToUpperInvariant())
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
