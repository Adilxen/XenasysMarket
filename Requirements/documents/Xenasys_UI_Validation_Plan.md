# Xenasys Market Place — UI Validation & UX Feedback Framework

**Document type:** Client-side form validation blueprint (pre–form implementation)  
**Aligned to:** `Xenasys_Implementation_Sequence.md` · `Xenasys_Database_Design.md` · BRD v1.1  
**Stack:** Next.js App Router · TypeScript · **React Hook Form** · **Zod** · Axios API client  

> **Visual contract:** Validation error text and invalid field borders use Tailwind `text-red-600` / `border-red-600` mapped to **`#DC2626`**. Do not use softer reds for schema failures.

---

## 1. Global UI validation lifecycle

### 1.1 Stack & ownership

| Layer | Responsibility |
|-------|----------------|
| **Zod schemas** | Single source of truth for shape, lengths, regex, cross-field rules |
| **React Hook Form (RHF)** | Field registration, focus/blur lifecycle, submit gating, error state |
| **`zodResolver`** | Bridge Zod ↔ RHF (`@hookform/resolvers/zod`) |
| **Server 422 mapper** | Merge API field errors into RHF `setError` without losing client schema state |

### 1.2 Lifecycle rules (mandatory)

#### A. On-Blur trigger

- Configure RHF with **`mode: "onBlur"`** (and optionally `reValidateMode: "onChange"` — see B).
- Each field runs its Zod path when the input loses focus (`onBlur`).
- Immediately show:
  - Border: `border-red-600` (`#DC2626`)
  - Message: `text-red-600` (`#DC2626`) under the control (BRD: inline red validation)

```tsx
const form = useForm<LoginInput>({
  resolver: zodResolver(loginSchema),
  mode: "onBlur",
  reValidateMode: "onChange",
  defaultValues: { /* ... */ },
});
```

#### B. On-Input clear

- After a field has been blurred once (touched + invalid), **`reValidateMode: "onChange"`** re-runs that field’s schema on every keystroke.
- As soon as the value satisfies Zod, RHF clears `errors[field]` → red border and message **disappear instantly**.
- Do **not** wait for the next blur to clear an error the user has already fixed.

#### C. On-Submit guard

- Primary submit control:
  - `type="submit"`
  - `disabled={form.formState.isSubmitting || !form.formState.isValid}` **or** rely on `handleSubmit` which **never** invokes the success callback while schema errors remain.
- Preferred pattern: always wrap with `form.handleSubmit(onValid, onInvalid)` so clicks are intercepted even if the button is not disabled (accessibility + keyboard Enter).
- `onInvalid`: optionally scroll to the first error via `form.setFocus(firstErrorKey)`.

```tsx
<button
  type="submit"
  disabled={isSubmitting}
  className="disabled:opacity-60"
>
  Save
</button>

<form onSubmit={handleSubmit(onValid, onInvalid)} noValidate>
  {/* native HTML5 validation OFF — Zod owns rules */}
</form>
```

### 1.3 Shared field chrome contract

```text
Valid / pristine:   border-zinc-300, no message
Invalid (touched):  border-red-600 (#DC2626), text-red-600 message under field
Disabled:           opacity-60, no new validation cycles
```

### 1.4 Shared packages

```bash
npm install react-hook-form zod @hookform/resolvers
```

### 1.5 Project layout (to implement later)

```text
frontend/src/
  lib/validation/
    messages.ts          # central #DC2626-facing copy constants
    primitives.ts        # reusable Zod helpers (trim, money, phone, ntn)
    map-api-errors.ts    # HTTP 422 → setError
  schemas/
    auth/login.schema.ts
    coa/master-account.schema.ts
    catalog/category.schema.ts
    catalog/sub-category.schema.ts
    catalog/brand.schema.ts
    products/product.schema.ts
    vendors/vendor.schema.ts
  components/forms/
    FormField.tsx        # label + input + error slot
    FormSelect.tsx
    FormFile.tsx
```

---

## 2. Shared Zod primitives & message catalog

### 2.1 Error message style

- Exact, actionable, BRD-aligned.
- Always render with `className="text-xs text-red-600"` → **`#DC2626`**.

### 2.2 Primitive builders (`lib/validation/primitives.ts`)

```ts
import { z } from "zod";

export const trimmedRequired = (label: string, max: number) =>
  z
    .string({ required_error: `${label} is required.` })
    .transform((v) => v.trim())
    .pipe(
      z
        .string()
        .min(1, `${label} is required.`)
        .max(max, `${label} must be at most ${max} characters.`),
    );

export const moneyPositive = (label: string) =>
  z.coerce
    .number({
      required_error: `${label} is required.`,
      invalid_type_error: `${label} must be a valid number.`,
    })
    .positive(`${label} must be greater than 0.00.`);

export const ntnSchema = z
  .string({ required_error: "NTN No is required." })
  .regex(/^[0-9]{7}-[0-9]{1}$/, "NTN No must match format 1234567-8.");

export const vendorPhoneSchema = z
  .string({ required_error: "Vendor Phone No is required." })
  .regex(
    /^[0-9+ -]{10,15}$/,
    "Vendor Phone No must be 10–15 characters (digits, spaces, +, -).",
  );

export const imageFileSchema = z
  .custom<File>((v) => v instanceof File, {
    message: "Category Image is required.",
  })
  .refine((f) => f.size <= 2 * 1024 * 1024, {
    message: "Image must be 2MB or smaller.",
  })
  .refine(
    (f) => {
      const name = f.name.toLowerCase();
      return (
        name.endsWith(".jpg") ||
        name.endsWith(".jpeg") ||
        name.endsWith(".png")
      );
    },
    { message: "Image must be JPG, JPEG, or PNG." },
  );
```

---

## 3. Milestone form field attributes (M0–M4)

Color for all messages below: **`#DC2626`** (`text-red-600`).

---

### M0 — Login form

**Route:** `/login`  
**Schema module:** `schemas/auth/login.schema.ts`

| Field | RHF name | Zod type / rules | Regex / extra | Exact error messages (`#DC2626`) |
|-------|----------|------------------|---------------|----------------------------------|
| User Name | `userName` | `string` → trim → min 1, max 50; allow alphanumeric, email, or username | Optional soft pattern: `^[\w.@+-]+$` | `User Name is required.` · `User Name must be at most 50 characters.` · `User Name contains invalid characters.` |
| Password | `password` | `string` min 8 (do **not** trim interior spaces) | — | `Password is required.` · `Password must be at least 8 characters.` |
| Company | `company_id` | positive INT (tenant PK); required selection from `/api/auth/companies` | Must match a loaded option id | `Company selection is required.` · `Select a valid company.` |

```ts
export const loginSchema = z.object({
  userName: z
    .string({ error: "User Name is required." })
    .trim()
    .min(1, "User Name is required.")
    .max(50, "User Name must be at most 50 characters.")
    .regex(/^[\w.@+-]+$/, "User Name contains invalid characters."),
  password: z
    .string({ error: "Password is required." })
    .min(1, "Password is required.")
    .min(8, "Password must be at least 8 characters."),
  company_id: z.coerce
    .number({ error: "Company selection is required." })
    .int("Select a valid company.")
    .positive("Company selection is required."),
});

export type LoginInput = z.infer<typeof loginSchema>;
```

**UX notes**

- Company dropdown shows **human-readable tenant names**; form state stores INT `company_id` for `POST /api/auth/login`.
- Submit locked while `!isValid` after first validation pass, or always gated by `handleSubmit`.

---

### M1 — Chart of Accounts forms

**Routes:** `/finance/coa`, `/finance/controls`  
**Schema modules:** `schemas/coa/*.schema.ts`

#### 3.1.1 Master Account

| Field | RHF name | Zod rules | Regex | Exact error messages (`#DC2626`) |
|-------|----------|-----------|-------|----------------------------------|
| Master Account Code | `code` | Numeric string only; **min 4 digits** | `^[0-9]{4,}$` | `Master Account Code is required.` · `Master Account Code must be numeric only.` · `Master Account Code must be at least 4 digits.` |
| Master Account Title | `title` | trim, required, max 100 | — | `Master Account Title is required.` · `Master Account Title must be at most 100 characters.` |
| Prefix | `prefix` | trim, required, max 5 | `^[A-Za-z0-9]+$` | `Prefix is required.` · `Prefix must be at most 5 characters.` · `Prefix must be alphanumeric.` |
| Active | `isActive` | `z.boolean()` default `true` | — | — |

```ts
export const masterAccountSchema = z.object({
  code: z
    .string({ required_error: "Master Account Code is required." })
    .trim()
    .regex(/^[0-9]+$/, "Master Account Code must be numeric only.")
    .min(4, "Master Account Code must be at least 4 digits."),
  title: trimmedRequired("Master Account Title", 100),
  prefix: z
    .string({ required_error: "Prefix is required." })
    .trim()
    .min(1, "Prefix is required.")
    .max(5, "Prefix must be at most 5 characters.")
    .regex(/^[A-Za-z0-9]+$/, "Prefix must be alphanumeric."),
  isActive: z.boolean().default(true),
});
```

#### 3.1.2 Control Account

| Field | RHF name | Zod rules | Exact error messages (`#DC2626`) |
|-------|----------|-----------|----------------------------------|
| Master Account | `masterAccountId` | required UUID (FK dropdown) | `Master Account is required.` |
| Control Account Title | `title` | trim, max 100 | `Control Account Title is required.` · `Control Account Title must be at most 100 characters.` |
| Control Kind | `controlKind` | enum `INVENTORY` \| `ACCOUNTS_PAYABLE` \| `OTHER` | `Control Kind is required.` |
| Code | `code` | system-generated → read-only in UI (no client edit) | — |

---

### M2 — Catalog forms (Category focus + siblings)

**Routes:** `/catalog/categories`, `/catalog/sub-categories`, `/catalog/brands`

#### 3.2.1 Category

| Field | RHF name | Zod rules | Regex / extra | Exact error messages (`#DC2626`) |
|-------|----------|-----------|---------------|----------------------------------|
| Category Name | `name` | trim, required, max 100; **async uniqueness** in tenant context | — | `Category Name is required.` · `Category Name must be at most 100 characters.` · `A category with this name already exists for your company.` |
| Description | `description` | optional, max 500, plain text | strip HTML | `Description must be at most 500 characters.` · `Description must be plain text only.` |
| Category Image | `image` | **File** required; ≤ **2MB**; extension **`.jpg` / `.jpeg` / `.png` only** | MIME optional secondary check `image/jpeg`, `image/png` | `Category Image is required.` · `Image must be 2MB or smaller.` · `Image must be JPG, JPEG, or PNG.` |
| Code | `code` | trim, required, max 20 (SKU segment) | `^[A-Za-z0-9-]+$` | `Category Code is required.` · `Category Code must be at most 20 characters.` |

```ts
export const categorySchema = z.object({
  name: trimmedRequired("Category Name", 100),
  code: z
    .string({ required_error: "Category Code is required." })
    .trim()
    .min(1, "Category Code is required.")
    .max(20, "Category Code must be at most 20 characters.")
    .regex(/^[A-Za-z0-9-]+$/, "Category Code contains invalid characters."),
  description: z
    .string()
    .max(500, "Description must be at most 500 characters.")
    .regex(/^[^<>]*$/, "Description must be plain text only.")
    .optional()
    .or(z.literal("")),
  image: imageFileSchema,
});
```

**Custom check — existing name (tenant context)**

- Use Zod `.superRefine` **or** RHF `trigger` + async validator after blur:
  1. Call `GET /api/categories/exists?name={name}` (or client cache of names from list query).
  2. If exists → `ctx.addIssue({ code: "custom", path: ["name"], message: "A category with this name already exists for your company." })`.
- Debounce async uniqueness **300ms** so blur/onChange does not spam the API (aligns with performance plan).

#### 3.2.2 Sub-Category

| Field | Zod highlights | Exact error messages (`#DC2626`) |
|-------|----------------|----------------------------------|
| `categoryId` | required UUID | `Category is required.` |
| `name` | max 100; unique within parent category | `Sub Category Name is required.` · `Sub Category Name must be unique within the selected category.` |
| `description` | optional max 500 | same as category |
| `image` | optional File; same 2MB / jpg|jpeg|png rules when present | same image messages |

#### 3.2.3 Brand

| Field | Zod highlights | Exact error messages (`#DC2626`) |
|-------|----------------|----------------------------------|
| `name` | max 100; unique in tenant | `Brand Name is required.` · `Brand Name must be at most 100 characters.` · `A brand with this name already exists for your company.` |
| `description` | optional max 500 | `Description must be at most 500 characters.` |

---

### M3 — Product form

**Routes:** `/products/new`, `/products/[id]/edit`  
**Schema:** `schemas/products/product.schema.ts`

| Field | RHF name | Zod rules | Cross-field / regex | Exact error messages (`#DC2626`) |
|-------|----------|-----------|---------------------|----------------------------------|
| Category | `categoryId` | required UUID | — | `Category is required.` |
| Sub Category | `subCategoryId` | required UUID; must belong to selected category (refine) | — | `Sub Category is required.` · `Sub Category must belong to the selected Category.` |
| Brand | `brandId` | required UUID | — | `Brand is required.` |
| Product Name | `productName` | trim, max 150 | — | `Product Name is required.` · `Product Name must be at most 150 characters.` |
| Description | `productDescription` | optional max 1000 | plain text | `Product Description must be at most 1000 characters.` |
| Packaging | `packaging` | trim, max 50 | — | `Packaging is required.` · `Packaging must be at most 50 characters.` |
| Unit | `unitId` | required UUID | — | `Unit of Measurement is required.` |
| Barcode | `barcode` | optional; alnum + hyphen; commas allowed | `^[A-Za-z0-9,-]*$` | `Barcode may only contain letters, numbers, commas, and hyphens.` |
| Minimum Stock | `minimumStockLevel` | int ≥ 0 | — | `Minimum Stock Level is required.` · `Minimum Stock Level must be an integer greater than or equal to 0.` |
| Invoice Price | `invoicePrice` | `decimal(10,2)` semantics; `> 0` | — | `Invoice Price is required.` · `Invoice Price must be greater than 0.00.` |
| Retail Price | `retailPrice` | `> 0` and **`>= invoicePrice`** | **Pricing guard** | `Retail Price is required.` · `Retail Price must be greater than 0.00.` · **`Retail Price must be greater than or equal to Invoice Price.`** |
| Product Image | `image` | File required; ≤2MB; jpg/jpeg/png | same as category | `Product Image is required.` · `Image must be 2MB or smaller.` · `Image must be JPG, JPEG, or PNG.` |
| Active | `isActive` | boolean default true | — | — |

```ts
export const productSchema = z
  .object({
    categoryId: z.string().uuid("Category is required."),
    subCategoryId: z.string().uuid("Sub Category is required."),
    brandId: z.string().uuid("Brand is required."),
    productName: trimmedRequired("Product Name", 150),
    productDescription: z
      .string()
      .max(1000, "Product Description must be at most 1000 characters.")
      .optional()
      .or(z.literal("")),
    packaging: trimmedRequired("Packaging", 50),
    unitId: z.string().uuid("Unit of Measurement is required."),
    barcode: z
      .string()
      .regex(
        /^[A-Za-z0-9,-]*$/,
        "Barcode may only contain letters, numbers, commas, and hyphens.",
      )
      .optional()
      .or(z.literal("")),
    minimumStockLevel: z.coerce
      .number({
        required_error: "Minimum Stock Level is required.",
        invalid_type_error: "Minimum Stock Level must be an integer greater than or equal to 0.",
      })
      .int("Minimum Stock Level must be an integer greater than or equal to 0.")
      .min(0, "Minimum Stock Level must be an integer greater than or equal to 0."),
    invoicePrice: moneyPositive("Invoice Price"),
    retailPrice: moneyPositive("Retail Price"),
    image: imageFileSchema,
    isActive: z.boolean().default(true),
  })
  .refine((d) => d.retailPrice >= d.invoicePrice, {
    path: ["retailPrice"],
    message: "Retail Price must be greater than or equal to Invoice Price.",
  });
```

**Pricing guard UX**

- On blur of either price field, `trigger(["invoicePrice", "retailPrice"])`.
- On change after touch, clear the cross-field error as soon as `retail >= invoice`.

---

### M4 — Vendor form

**Routes:** `/vendors/new`, `/vendors/[id]`  
**Schema:** `schemas/vendors/vendor.schema.ts`

| Field | RHF name | Zod rules | Regex | Exact error messages (`#DC2626`) |
|-------|----------|-----------|-------|----------------------------------|
| Vendor Name | `vendorName` | trim, max 100 | — | `Vendor Name is required.` · `Vendor Name must be at most 100 characters.` |
| NTN No | `ntnNo` | required mask | **`^[0-9]{7}-[0-9]{1}$`** | `NTN No is required.` · **`NTN No must match format 1234567-8.`** |
| Address | `address` | trim, max 250 | — | `Address is required.` · `Address must be at most 250 characters.` |
| Vendor Phone No | `vendorPhoneNo` | required | **`^[0-9+ -]{10,15}$`** | `Vendor Phone No is required.` · **`Vendor Phone No must be 10–15 characters (digits, spaces, +, -).`** |
| Email | `email` | optional email | standard email | `Enter a valid email address (name@domain.com).` |
| Representative Name | `representativeName` | trim, max 100 | — | `Representative Name is required.` · `Representative Name must be at most 100 characters.` |
| Representative No | `representativeNo` | phone-like required | recommend same phone regex or `^[0-9+ -]{10,15}$` | `Representative No is required.` · `Representative No must be a valid phone number.` |

```ts
export const vendorSchema = z.object({
  vendorName: trimmedRequired("Vendor Name", 100),
  ntnNo: z
    .string({ required_error: "NTN No is required." })
    .regex(/^[0-9]{7}-[0-9]{1}$/, "NTN No must match format 1234567-8."),
  address: trimmedRequired("Address", 250),
  vendorPhoneNo: z
    .string({ required_error: "Vendor Phone No is required." })
    .regex(
      /^[0-9+ -]{10,15}$/,
      "Vendor Phone No must be 10–15 characters (digits, spaces, +, -).",
    ),
  email: z
    .string()
    .email("Enter a valid email address (name@domain.com).")
    .optional()
    .or(z.literal("")),
  representativeName: trimmedRequired("Representative Name", 100),
  representativeNo: z
    .string({ required_error: "Representative No is required." })
    .regex(
      /^[0-9+ -]{10,15}$/,
      "Representative No must be a valid phone number.",
    ),
});
```

**NTN mask UX (recommended)**

- Input mask `#######-#` while typing; still validate final value with the regex above on blur/submit.

---

## 4. Backend error mapping integration (HTTP 422)

### 4.1 .NET contract (expected)

BRD requires **HTTP 422 Unprocessable Entity** with **structured field-level** details.

Canonical client shape (camelCase JSON):

```json
{
  "errors": {
    "userName": ["User Name is required."],
    "retailPrice": ["Retail Price must be greater than or equal to Invoice Price."],
    "ntnNo": ["NTN No must be unique per tenant."]
  }
}
```

Also accept ASP.NET `ValidationProblemDetails`:

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "ProductName": ["Product Name is required."],
    "InvoicePrice": ["Invoice Price must be greater than 0.00."]
  }
}
```

### 4.2 Mapper utility

```ts
// lib/validation/map-api-errors.ts
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";

export type ApiFieldErrors = {
  errors?: Record<string, string[]>;
};

/** Map ASP.NET / BRD 422 payloads onto RHF field errors (#DC2626 UI). */
export function mapApiErrorsToForm<T extends FieldValues>(
  data: unknown,
  setError: UseFormSetError<T>,
  /** optional: ProductName → productName */
  normalizeKey: (key: string) => string = toCamelCase,
): void {
  const bag = (data as ApiFieldErrors)?.errors;
  if (!bag) return;

  for (const [rawKey, messages] of Object.entries(bag)) {
    const key = normalizeKey(rawKey) as Path<T>;
    const message = messages?.[0] ?? "Invalid value.";
    setError(key, { type: "server", message });
  }
}

function toCamelCase(key: string): string {
  return key.length
    ? key[0]!.toLowerCase() + key.slice(1)
    : key;
}
```

### 4.3 Integration in submit handlers

```ts
async function onValid(values: ProductInput) {
  try {
    await apiPost("/api/products", values);
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.status === 422) {
      mapApiErrorsToForm(err.response.data, setError);
      const first = Object.keys(err.response.data?.errors ?? {})[0];
      if (first) setFocus(toCamelCase(first) as Path<ProductInput>);
      return;
    }
    // non-422: toast / boundary
  }
}
```

### 4.4 Distribution rules

| Rule | Behavior |
|------|----------|
| Field key matches RHF `name` | Message appears **beneath that input** via shared `FormField` error slot |
| Unknown key | Surface under form-level `root` / `setError("root", { message })` |
| Multiple messages per field | Show **first** message only (keep UI calm); full array available for logging |
| After server error | Keep `reValidateMode: "onChange"` so typing clears **client** issues; server error clears when `trigger(field)` succeeds or on next successful submit |
| Precedence | Client Zod errors block submit first; 422 only after a request was sent |

### 4.5 `FormField` error slot (contract)

```tsx
{error ? (
  <span className="block text-xs text-red-600" role="alert">
    {error.message}
  </span>
) : null}
// text-red-600 === #DC2626
```

Invalid input class merge:

```tsx
className={cn(
  "w-full border px-3 py-2 text-sm outline-none",
  error ? "border-red-600" : "border-zinc-300 focus:border-[#2563EB]",
)}
```


---

## 5. Accessibility & UX checklist

- [ ] `noValidate` on `<form>` — Zod/RHF own validation  
- [ ] Errors use `role="alert"` or `aria-describedby` linked to input `id`  
- [ ] `aria-invalid={!!error}` on controls  
- [ ] Focus first invalid field on submit failure  
- [ ] Password fields never echo values in error text  
- [ ] File inputs show filename + size hint before submit  
- [ ] Dropdown empty value (`""`) fails required UUID/refined checks on blur  

---

## 6. Implementation sequence (forms)

Align with master roadmap — schemas before screens:

1. Install RHF + Zod + resolvers; add `FormField` / `mapApiErrorsToForm`.  
2. **M0** Login schema + wire `/login` (replace ad-hoc state).  
3. **M1** Master/Control schemas with numeric code rules.  
4. **M2** Category (+ image File schema) + async name uniqueness.  
5. **M3** Product schema with **Retail ≥ Invoice** refine.  
6. **M4** Vendor schema with **NTN** + **phone** regex.  

Do **not** implement Product/Vendor forms until M1/M2 data dependencies exist (per `Xenasys_Implementation_Sequence.md`).

---

## 7. Approval gate

Before coding individual forms:

- [ ] Lifecycle trio accepted: **onBlur validate · onChange clear · submit guard**  
- [ ] Error color locked to **`#DC2626`**  
- [ ] Zod schemas listed for M0–M4 accepted as SSOT  
- [ ] 422 → `setError` mapper accepted for .NET integration  
- [ ] **Listing + Form chrome** follows `Xenasys_Listing_Form_UI_Pattern.md` (Category Listing / Add Category drawer)  

---

## 8. Listing & form visual pattern (locked screens)

**Authoritative visual SSoT:** [`Xenasys_Listing_Form_UI_Pattern.md`](./Xenasys_Listing_Form_UI_Pattern.md)

| Surface | Pattern |
|---------|---------|
| **Listing** | Breadcrumb → title → Export (secondary) + Add (primary blue) → white data card → table (horizontal dividers only) → blue pagination active |
| **Form** | Right **slide-over drawer** + dim overlay; required `*`; gray borders; blue focus; dashed soft-blue upload; Cancel + Save blue footer |
| **Banned** | Emerald/green CTAs, green focus rings, floating unstructured errors |

**Code tokens:** `frontend/src/app/globals.css` · `frontend/src/lib/forms/field-styles.ts`  
**Cursor rule:** `.cursor/rules/xenasys-listing-form-ui.mdc`

---

*End of UI Validation Plan. Next step after approval: implement `lib/validation/*` + migrate Login to React Hook Form + Zod.*
