/**
 * ==========================================================================================
 * WHAT: Title-case helpers for listing / spreadsheet column headings.
 * WHY: Ported from newSalon DataTable + SpreadsheetGrid heading formatting.
 * EXAMPLE: formatHeadingTitle("role code") → "Role Code"
 * ==========================================================================================
 */

const PRESERVED_ACRONYMS = new Set([
  "API",
  "COA",
  "FOC",
  "GST",
  "HR",
  "ID",
  "OTP",
  "PIN",
  "PO",
  "PR",
  "QR",
  "SKU",
  "SMS",
  "URL",
  "VAT",
]);

function capitalizeHeadingToken(token: string): string {
  if (!token) return token;

  if (token.includes("-")) {
    return token.split("-").map(capitalizeHeadingToken).join("-");
  }

  const prefix = token.match(/^[^\w]*/)?.[0] ?? "";
  const suffix = token.match(/[^\w]*$/)?.[0] ?? "";
  const core = token.slice(prefix.length, token.length - suffix.length);

  if (!core) return token;

  const coreUpper = core.toUpperCase();
  if (/^[A-Z]{2,}$/.test(core) || PRESERVED_ACRONYMS.has(coreUpper)) {
    return `${prefix}${coreUpper}${suffix}`;
  }

  return `${prefix}${core.charAt(0).toUpperCase()}${core.slice(1).toLowerCase()}${suffix}`;
}

export function formatHeadingTitle(value: string): string {
  const s = value == null ? "" : String(value);
  const trimmed = s.trim();
  if (!trimmed) return "";

  return trimmed.split(/\s+/).map(capitalizeHeadingToken).join(" ");
}
