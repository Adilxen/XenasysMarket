/**
 * ==========================================================================================
 * WHAT: Shared Zod helpers that reject HTML / script-like user input.
 * WHY: BRD input sanitization — stop XSS markup before it reaches API write paths.
 * EXAMPLE: zNoHtmlString().parse("Hi") ok; "<script>" fails with field message.
 * ==========================================================================================
 */
import { z } from "zod";

const DANGEROUS_MARKUP = /<[^>]*>|javascript\s*:|on\w+\s*=/i;

export function hasDangerousMarkup(value: string): boolean {
  return DANGEROUS_MARKUP.test(value);
}

export function zNoHtmlString(message = "HTML tags and script content are not allowed.") {
  return z.string().refine((v) => !hasDangerousMarkup(v), { message });
}
