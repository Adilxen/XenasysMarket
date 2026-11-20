/**
 * ==========================================================================================
 * WHAT: Create / edit role Zod schema — Name + optional Description + DefaultRoute.
 * WHY: Post-login landing is set per role (T1.40); Code never user-entered.
 * EXAMPLE: roleFormSchema.parse({ name: "Buyer", defaultRoute: "/products" })
 * ==========================================================================================
 */

import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

export const roleFormSchema = z.object({
  name: zNoHtmlString()
    .trim()
    .min(2, "Role name is required.")
    .max(100, "Max 100 characters."),
  description: zNoHtmlString().trim().max(500, "Max 500 characters.").optional().or(z.literal("")),
  defaultRoute: z
    .string()
    .trim()
    .max(200, "Max 200 characters.")
    .refine((v) => !v || v.startsWith("/"), {
      message: "Default route must start with /.",
    })
    .optional()
    .or(z.literal("")),
  /** Super Admin create — target company; Tenant Admin omits. */
  companyId: z.number().int().positive().nullable().optional(),
  isActive: z.boolean().optional(),
});

export type RoleFormInput = z.infer<typeof roleFormSchema>;

/** @deprecated Legacy create schema — prefer roleFormSchema. */
export const createRoleSchema = roleFormSchema.extend({
  code: z.string().optional(),
  permissionCodes: z.array(z.string()).optional(),
});

export type CreateRoleInput = z.infer<typeof createRoleSchema>;

export function normalizeRoleCode(raw: string): string {
  return raw
    .toUpperCase()
    .replace(/\s+/g, "_")
    .replace(/[^A-Z0-9_]/g, "");
}

export function normalizeDefaultRoute(raw: string | null | undefined): string {
  const r = (raw ?? "").trim();
  if (!r.startsWith("/") || r.startsWith("//")) return "/dashboard";
  return r.length > 200 ? r.slice(0, 200) : r;
}
