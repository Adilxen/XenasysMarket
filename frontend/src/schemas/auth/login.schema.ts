import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

/**
 * ==========================================================================================
 * WHAT: Zod schema for login — userName, password, and company_id as positive INT PK.
 * WHY: Powers RHF+Zod onBlur validation; messages surface in the UI as #DC2626 field errors.
 * EXAMPLE: { userName: "subhan", password: "secret123", company_id: 12 } passes; missing INT fails.
 * ==========================================================================================
 */
export const loginSchema = z.object({
  userName: zNoHtmlString()
    .trim()
    .min(1, "User Name is required.")
    .max(50, "User Name must be at most 50 characters.")
    .regex(/^[\w.@+-]+$/, "User Name contains invalid characters."),
  password: z
    .string({ error: "Password is required." })
    .min(1, "Password is required.")
    .min(8, "Password must be at least 8 characters."),
  company_id: z
    .number({ error: "Company selection is required." })
    .int("Select a valid company.")
    .positive("Company selection is required."),
});

export type LoginInput = z.infer<typeof loginSchema>;
