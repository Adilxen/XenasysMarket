# Xenasys Market Place — Master Implementation Sequence

**Document status:** Baseline roadmap (execute step-by-step)  
**Sources:** BRD v1.1 (`Xenasys_Market_Place_BRD_v1.1.docx`) + architectural dependency gates  
**Stack:** Next.js (App Router / TypeScript) · .NET Core Web API (Clean Architecture) · EF Core  

> **Note:** `Xenasys_Cursor_Context.md` was not present in the workspace at audit time. This sequence is baselined from the BRD and the hard dependency rules below. Re-validate if that context file is added later.

---

## 1. Absolute dependency constraints

These rules override convenience or “build UI first” impulses:

1. **Data constraints (Product structure)**  
   A **Product** cannot exist without:
   - Category  
   - Sub-Category (parent Category must exist first)  
   - Brand  
   - Unit of Measurement (system table)

2. **Transaction integrity (COA automation)**  
   A **Product** or **Vendor** cannot be successfully committed without a pre-existing **3-tier Chart of Accounts**:
   - Master Account → Control Account → Subsidiary Account  
   On save, background automation **must** insert a Subsidiary Account under the correct Control node:
   - Product → under **Inventory Control Account**  
   - Vendor → under **Accounts Payable Control Account**

3. **Multi-tenant isolation**  
   Every tenant-scoped query must use `company_id` / `tenant_id` from the validated **JWT**, never from client payload.

---

## 2. Critical path (shortest time-to-Product)

```
M0 Tenancy & Auth
  → M1 Chart of Accounts (seed Inventory Control + AP Control)
  → M2 Catalog Masters (Category → Sub-Category → Brand → Units)
  → M3 Product (POST + SKU + inventory subsidiary in one DB transaction)
```

**Parallel after M1:** M4 Vendor may run beside M2/M3 (Vendor only needs COA, not catalog masters).

**Do not start:** M5 Stock or M7 Super Admin aggregations until M3 has real write paths.

---

## 3. Dependency DAG (must-precede)

```
M0 ──► M1 ──► M3 ──► M5 ──► M7
 │      │      ▲            ▲
 │      └──► M4 ────────────┤
 │             ▲            │
 └──► M2 ──────┘            │
 │                          │
 └──► M6 (RBAC, parallel) ──┘
```

| Edge | Meaning |
|------|---------|
| M0 → M1, M2, M6 | Tenancy/auth before any tenant-scoped module |
| M1 → M3, M4 | COA required before Product/Vendor commit |
| M2 → M3 | Catalog masters required before Product |
| M3 → M5 | Stock requires Product rows |
| M3/M4/M5/M6 → M7 | Aggregations need real multi-tenant data |

---

## 4. Anti-patterns (rejected sequences)

| Rejected order | Failure mode |
|----------------|--------------|
| Product before COA | AFTER INSERT subsidiary has no Control parent → broken TX / orphan GL |
| Product before Category / Sub-Category / Brand | FK violation or null structural codes → invalid SKU |
| Vendor before COA | AP subsidiary automation cannot attach under Accounts Payable Control |
| Sub-Category before Category | Parent FK missing; cascading dropdown invalid |
| Super Admin views before M3/M4 data | Empty aggregations; premature view/index design |

---

## 5. Milestones (execute in order)

### M0 — Platform identity, tenancy & security kernel

**Status:** Partial (skeleton exists)

**Why this precedes the next step:**  
Every subsequent table is company-scoped. JWT → `ITenantProvider` → EF global filters must exist before tenant CRUD, or cross-tenant leakage gets baked into the codebase.

#### Migrations / tables

- `tenants`
- `users` / `user_tenant_memberships`
- `roles`, `permissions`, `role_permissions`
- Audit column convention on all tables: `insert_date`, `insert_user_id`, `update_date`, `update_user_id`

#### API contracts

```http
POST   /api/auth/login
GET    /api/tenant/me
POST   /api/admin/tenants          # Super Admin
CRUD   /api/tenant/users
CRUD   /api/tenant/roles
```

#### Next.js views

- `/login`
- `/dashboard` (shell)
- `TenantShell` + `AppProviders` (Query client, auth gate)

---

### M1 — 3-tier Chart of Accounts (**HARD GATE** for Product & Vendor)

**Status:** Not started — **must precede M3 and M4**

**Why this precedes the next step:**  
BRD triggers create Subsidiary rows under Inventory Control (Product) and Accounts Payable Control (Vendor) at save time. Without Master → Control nodes already in the database, those automations cannot commit transactionally. Product/Vendor saves must fail closed until COA is ready.

#### Migrations / tables

```text
master_accounts
  - Id, Code, Title, Prefix, IsActive, audit columns
  - Owned/defined at platform level (Super Admin)

control_accounts
  - Id, MasterAccountId (FK), Code, Title, audit columns
  - Seed: "Inventory Control", "Accounts Payable"

subsidiary_accounts
  - Id, CompanyId, ControlAccountId (FK)
  - SubsidiaryAccountCode (unique per tenant)
  - SubsidiaryAccountTitle
  - AccountNo (CHAR(12) = TenantCode + seq) + AccountCode (TenantCode-M/P/V##### or -CASH/-BANK); both system-allocated, immutable
  - IsSystemGenerated, IsActive, audit columns

Indexes:
  - (company_id, id)                          -- keyset pagination
  - UNIQUE (company_id, subsidiary_account_code)
  - UNIQUE (company_id, account_no)
```

#### Seed requirement (blocking)

Before any Product/Vendor write:

1. Master accounts exist (e.g. Assets, Liabilities).  
2. Control accounts exist: **Inventory Control**, **Accounts Payable**.  
3. Per-tenant resolution path is defined (shared template vs tenant-provisioned controls).

#### API contracts

```http
GET/POST  /api/coa/masters              # Super Admin write; tenants read-only
GET/POST  /api/coa/controls
GET       /api/coa/subsidiaries?afterId={guid}&limit={n}   # keyset
# DELETE subsidiary blocked when IsSystemGenerated = true
```

#### Next.js views

- `/finance/coa` — tree: Master → Control → Subsidiary  
- `/finance/controls`  
- Master chart: read-only for Tenant Admin  

---

### M2 — Catalog structural masters (**HARD GATE** for Product)

**Status:** Stub `GET /api/categories` only — expand before Product write

**Why this precedes the next step:**  
A Product cannot exist without Category, Sub-Category, Brand, and Unit. Sub-Category FK requires Category first. SKU formula concatenates Category / Sub-Category / Unit codes — those codes must be stable before Product insert.

#### Internal order within M2

```
Units (system) → Categories → Sub-Categories → Brands
```

#### Migrations / tables

```text
units
  - Id, Code, Name   # Pcs, Kg, Ltr, Box, Pack

categories
  - Id, CompanyId, Name (UNIQUE per company, case-insensitive)
  - Description, ImagePath, IsActive, audit columns
  - UNIQUE (company_id, category_name)

sub_categories
  - Id, CompanyId, CategoryId (FK)
  - Name (UNIQUE within parent category)
  - Description, ImagePath, audit columns

brands
  - Id, CompanyId, Name (unique within tenant)
  - Description, audit columns
```

#### API contracts

```http
CRUD   /api/units                    # mostly read
CRUD   /api/categories
CRUD   /api/sub-categories?categoryId={guid}
CRUD   /api/brands
GET    /api/sub-categories?categoryId={guid}   # cascading dropdown
```

#### Next.js views

- `/catalog/categories`
- `/catalog/sub-categories`
- `/catalog/brands`
- Replace stub category list used by product form  

---

### M3 — Product listing, SKU automation & inventory subsidiary

**Status:** Read / active-toggle skeleton exists — **write path blocked on M1 + M2**

**Why this precedes the next step:**  
First module that consumes **both** hard gates. Inside **one DB transaction**:

1. Validate FKs (Category, Sub-Category, Brand, Unit, tenant)  
2. Generate SKU  
3. Insert Product  
4. Insert Subsidiary under Inventory Control  
5. Link `SubsidiaryAccountId` on Product  

Skipping M1 or M2 breaks integrity or leaves orphan products.

#### Migrations / tables

```text
products
  - Id, CompanyId
  - CategoryId, SubCategoryId, BrandId
  - ProductName, ProductDescription
  - Packaging, UnitOfMeasurement / UnitId
  - Sku (generated), Barcode
  - MinimumStockLevel
  - InvoicePrice DECIMAL(10,2), RetailPrice DECIMAL(10,2)
  - ImagePath, IsActive
  - SubsidiaryAccountId (FK, set on save)
  - audit columns

Indexes:
  - (company_id, id)                 -- keyset
  - UNIQUE (company_id, sku)
```

#### SKU formula (BRD)

```text
[Category Code]-[SubCategory Code]-[Product Name Short Code]-[Packaging]-[Unit Code]
Example: CAT01-SUB04-PROD88-BOX12-PCS
```

#### Subsidiary title (product)

```text
[Product Name]
```

SKU is stored on `Product.ProductCode` / subsidiary `ProductCode`, not concatenated into the title.

#### API contracts

```http
GET    /api/products?afterId={guid}&limit={n}     # exists
GET    /api/products/{id}                         # exists
POST   /api/products                              # transaction + triggers (to build)
PUT    /api/products/{id}
PUT    /api/products/{id}/active                  # exists (optimistic UI)
```

#### Next.js views

- `/products` — virtualized + keyset (exists)  
- `/products/new` — wire real `POST`  
- `/products/[id]/edit`  

---

### M4 — Vendor registration & AP subsidiary

**Status:** Not started  

**Why this can parallel M2/M3:**  
Depends on **M1 only** (Accounts Payable Control Account). Independent of Category/Brand. Still cannot ship before COA seed.

#### Migrations / tables

```text
vendors
  - Id, CompanyId
  - VendorName, NtnNo (format 1234567-8, unique per tenant)
  - Address, VendorPhoneNo, Email
  - RepresentativeName, RepresentativeNo
  - SubsidiaryAccountId (FK, set on save)
  - audit columns
```

#### Subsidiary title (BRD)

```text
Vendor: [Vendor Name]
```

#### API contracts

```http
GET     /api/vendors?afterId={guid}&limit={n}
GET     /api/vendors/{id}
POST    /api/vendors          # creates AP subsidiary in same TX
PUT     /api/vendors/{id}
DELETE  /api/vendors/{id}     # respect system subsidiary delete rules
```

#### Next.js views

- `/vendors`  
- `/vendors/new`  
- `/vendors/[id]`  

---

### M5 — Stock levels & minimum-stock alerts

**Status:** After M3  

**Why this precedes M7 (and follows M3):**  
Stock mutations reference Product rows and thresholds. Alerts without Product master are meaningless.

#### Migrations / tables

```text
# Option A: quantity columns on products
# Option B: stock_levels + stock_movements ledger

stock_levels / product qty fields
stock_movements (optional)
notification_outbox (low-stock events)
```

#### API contracts

```http
PATCH  /api/products/{id}/stock
GET    /api/stock/alerts
```

#### Next.js views

- `/inventory/stock`  
- Dashboard low-stock widget  

---

### M6 — Granular RBAC enforcement (parallel track)

**Status:** Parallel with M1–M5; finish matrix after routes exist  

**Why not blocking M1:**  
Role tables can start in M0, but the BRD §3.2 permission matrix is only fully testable once Category / Product / Vendor / COA routes exist. Harden authorization middleware continuously.

#### Migrations / tables

- Permission seed aligned to BRD §3.2 matrix (Super Admin / Tenant Admin / Operational roles)

#### API contracts

- Authorization middleware on **every** route  
- Example gate: reject if not `SUPER_ADMIN` and `resource.company_id != user.company_id`

#### Next.js views

- `/admin/roles`  
- Permission matrix UI  

---

### M7 — Super Admin cross-tenant aggregation

**Status:** Last among core BRD modules  

**Why last:**  
Views need real multi-tenant product / stock / vendor data. Building dashboards earlier yields empty shells and wrong query shapes.

#### Migrations / views

```sql
-- BRD Trigger 4 examples
vw_super_admin_inventory_summary
vw_super_admin_sales_by_tenant

-- Plus central audit_log for Super Admin operations
```

#### API contracts

```http
GET  /api/super/inventory-summary
GET  /api/super/sales-by-tenant
```

#### Next.js views

- `/super/dashboard`  
- Per-tenant selector  

---

## 6. Recommended next sprint (immediate)

1. **Close M0 gaps** — real users, tenant seed, bcrypt password verify (replace login skeleton).  
2. **Ship M1** — migrations for `master_accounts`, `control_accounts`, `subsidiary_accounts` + seed Inventory Control & Accounts Payable.  
3. **Ship M2** — Category → Sub-Category → Brand → Units (replace category stub).  
4. **Only then** enable `POST /api/products` with transactional SKU + subsidiary creation.  
5. **Parallel:** start M4 Vendor API/UI once M1 seed is live.

---

## 7. Execution checklist

Use this as the master tick-list:

- [ ] **M0** Tenancy, auth, users/roles, audit convention  
- [ ] **M1** COA tables + Inventory Control & AP Control seed  
- [ ] **M2** Units → Categories → Sub-Categories → Brands  
- [ ] **M3** Product write path (SKU + inventory subsidiary TX)  
- [ ] **M4** Vendor write path (AP subsidiary TX)  
- [ ] **M5** Stock & minimum-stock alerts  
- [ ] **M6** Full RBAC matrix on all module routes  
- [ ] **M7** Super Admin aggregation views & UI  

---

## 8. Related workspace artifacts

| Artifact | Role |
|----------|------|
| `Xenasys_Market_Place_BRD_v1.1.docx` | Authoritative functional/technical requirements |
| `backend/src/*` | Clean Architecture API skeleton |
| `frontend/src/*` | Next.js App Router + performance patterns |
| Canvas: `xenasys-module-roadmap` | Visual dependency DAG (IDE canvas) |

---

*End of master developmental roadmap. Execute milestones in order; do not skip M1 or M2 before Product writes.*
