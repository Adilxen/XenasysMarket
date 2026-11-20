using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Xenasys.Core.Validation;

// ==========================================================================================
// WHAT: DataAnnotation that rejects HTML tags / script-like payloads in string inputs.
// WHY: BRD global rule — input sanitization against XSS / markup injection on write paths.
// EXAMPLE: "Hello <script>" → validation error; "Hello world" → pass.
// ==========================================================================================
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed partial class NoHtmlOrScriptAttribute : ValidationAttribute
{
    private static readonly Regex DangerousMarkup = DangerousMarkupRegex();

    public NoHtmlOrScriptAttribute()
        : base("HTML tags and script content are not allowed.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text)
        {
            return true;
        }

        return !DangerousMarkup.IsMatch(text);
    }

    [GeneratedRegex(@"<[^>]*>|javascript\s*:|on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousMarkupRegex();
}
