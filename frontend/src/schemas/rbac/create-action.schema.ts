/**
 * ==========================================================================================
 * WHAT: Create / edit Action Zod schema (Company-pattern).
 * WHY: Name required; no auto-code in T1.26.
 * EXAMPLE: actionFormSchema.parse({ name: "Export", isActive: true })
 * ==========================================================================================
 */

import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

export const actionFormSchema = z.object({
  name: zNoHtmlString()
    .trim()
    .min(2, "Action name is required.")
    .max(50, "Max 50 characters."),
  isActive: z.boolean().optional(),
});

export type ActionFormInput = z.infer<typeof actionFormSchema>;
