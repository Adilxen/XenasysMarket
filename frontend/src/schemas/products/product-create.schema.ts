import { z } from "zod";
import { zNoHtmlString } from "@/lib/security/sanitize";

/**
 * ==========================================================================================
 * WHAT: Product create/update Zod schema — mandatory catalog FKs, prices, no HTML.
 * WHY: BRD §4.5 — Category+Sub+Brand+Unit required; Retail >= Invoice.
 * EXAMPLE: categoryId/subCategoryId/brandId/unitId must be positive INT from selects.
 * ==========================================================================================
 */
export const productCreateSchema = z
  .object({
    productName: zNoHtmlString()
      .trim()
      .min(1, "Product Name is mandatory.")
      .max(150, "Max length 150 chars."),
    packaging: zNoHtmlString()
      .trim()
      .min(1, "Packaging is mandatory.")
      .max(50, "Max length 50 chars."),
    categoryId: z
      .number({ error: "Category is mandatory." })
      .int("Select a valid category.")
      .positive("Category is mandatory."),
    subCategoryId: z
      .number({ error: "Sub Category is mandatory." })
      .int("Select a valid sub category.")
      .positive("Sub Category is mandatory."),
    brandId: z
      .number({ error: "Brand is mandatory." })
      .int("Select a valid brand.")
      .positive("Brand is mandatory."),
    unitId: z
      .number({ error: "Unit is mandatory." })
      .int("Select a valid unit.")
      .positive("Unit is mandatory."),
    invoicePrice: z.coerce.number().positive("Invoice Price must be > 0."),
    retailPrice: z.coerce.number().positive("Retail Price must be > 0."),
    minimumStockLevel: z.coerce
      .number()
      .int("Minimum Stock must be an integer >= 0.")
      .min(0, "Minimum Stock must be an integer >= 0."),
    barcode: z
      .string()
      .trim()
      .max(500)
      .regex(
        /^[A-Za-z0-9,-]*$/,
        "Barcode may only contain letters, numbers, commas, and hyphens.",
      )
      .optional()
      .or(z.literal("")),
    productDescription: z.string().trim().max(1000).optional().or(z.literal("")),
  })
  .refine((d) => d.retailPrice >= d.invoicePrice, {
    message: "Retail Price must be >= Invoice Price.",
    path: ["retailPrice"],
  });

export type ProductCreateInput = z.infer<typeof productCreateSchema>;
