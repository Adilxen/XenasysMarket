/**
 * ==========================================================================================
 * WHAT: Create / edit Form Zod schema (Company-pattern).
 * WHY: Form Name + Path required for permission-management navigation catalog.
 * EXAMPLE: formCatalogSchema.parse({ name: "Roles", path: "/settings/roles" })
 * ==========================================================================================
 */

import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

export const formCatalogSchema = z.object({
  name: zNoHtmlString()
    .trim()
    .min(2, "Form name is required.")
    .max(100, "Max 100 characters."),
  path: zNoHtmlString()
    .trim()
    .min(1, "Form path is required.")
    .max(200, "Max 200 characters.")
    .refine((v) => v.startsWith("/"), "Path must start with '/'")
    .refine((v) => !/\s/.test(v), "Path cannot contain spaces"),
  isActive: z.boolean().optional(),
});

export type FormCatalogInput = z.infer<typeof formCatalogSchema>;
