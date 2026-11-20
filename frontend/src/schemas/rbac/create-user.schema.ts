/**
 * ==========================================================================================
 * WHAT: Users create/edit Zod schema — multi-role (T1.38) + password rules by mode.
 * WHY: newSalon-style RoleIds[]; at least one role required.
 * EXAMPLE: userFormSchema with mode=create → password + roleIds min 1.
 * ==========================================================================================
 */

import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

export const userFormSchema = z
  .object({
    mode: z.enum(["create", "edit", "view"]),
    userName: zNoHtmlString()
      .trim()
      .min(1, "User Name is required.")
      .max(50, "Max 50 characters."),
    email: z
      .string()
      .trim()
      .email("Invalid email.")
      .max(100, "Max 100 characters.")
      .optional()
      .or(z.literal("")),
    password: z.string().optional().default(""),
    /** Super Admin create — target company; Tenant Admin omits. */
    companyId: z.number().int().positive().nullable().optional(),
    roleIds: z.array(z.number().int().positive()).default([]),
    membershipIsActive: z.boolean().optional(),
  })
  .superRefine((data, ctx) => {
    const pwd = data.password ?? "";
    if (data.mode === "create") {
      if (pwd.length < 8) {
        ctx.addIssue({
          code: "custom",
          path: ["password"],
          message: "Password must be at least 8 characters.",
        });
      }
      if (!data.roleIds || data.roleIds.length < 1) {
        ctx.addIssue({
          code: "custom",
          path: ["roleIds"],
          message: "Select at least one role.",
        });
      }
      return;
    }
    if (data.mode === "edit" && pwd.trim() !== "" && pwd.length < 8) {
      ctx.addIssue({
        code: "custom",
        path: ["password"],
        message: "Password must be at least 8 characters.",
      });
    }
  });

export type UserFormInput = z.infer<typeof userFormSchema>;
