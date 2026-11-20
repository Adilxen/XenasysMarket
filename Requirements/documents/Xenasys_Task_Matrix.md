# Xenasys Market Place — Master Task Matrix (Execution Contract)

**Document type:** System integrity ledger · regression control · AI/developer gate  
**Authority:** Overrides convenience refactors when they conflict with baselined rules  
**Baselines:**  
- `Xenasys_Implementation_Sequence.md`  
- `Xenasys_Database_Design.md`  
- `Xenasys_UI_Validation_Plan.md`  
- `Xenasys_Market_Place_BRD_v1.1.docx`  

**Scale posture:** Daraz-class load — no feature may introduce full-table scans, OFFSET deep paging, sync heavy work on the HTTP path, or bypass of COA/SKU/tenant automations.

---

## 1. CURSOR PRE-IMPLEMENTATION GUARDRAIL (CRITICAL GATE)

### 1.1 Mandatory read-before-write rule

> **STOP — READ THIS FIRST.**  
> Any AI Agent (including Cursor agents) or human developer working in this repository **MUST**, **BEFORE starting ANY new task** (feature, endpoint, migration, UI form, refactor, or “small fix”):
>
> 1. Open and cross-reference **`Xenasys_Task_Matrix.md`** (this file).  
> 2. Cross-check related rows in `Xenasys_Implementation_Sequence.md`, `Xenasys_Database_Design.md`, and `Xenasys_UI_Validation_Plan.md`.  
> 3. Append a new row to the **Regression Compliance Audit Log** (§3) with `Pre-Check Conflict Status` filled **before writing code**.

Failure to perform this gate is a process violation. Code landed without a log row is subject to immediate revert.

### 1.2 Logical evaluation check (required)

Before coding, the agent/developer **MUST** answer explicitly:

> **Does the new feature or endpoint modify, override, or bypass an existing data constraint or automated database trigger listed in this matrix?**

| Answer | Action |
|--------|--------|
| **No conflict** | Set `Pre-Check Conflict Status = Passed`. Proceed only after the audit-log row exists. |
| **Conflict / unsure** | Set `Pre-Check Conflict Status = Failed`. **STOP development immediately.** Raise an **architectural exception flag** (document in PR/chat: conflicting Task IDs + proposed resolution). Do **not** “temporarily” disable triggers, remove CHECK constraints, trust client `company_id`, or skip transactional SKU/COA inserts. |

### 1.3 Non-negotiable hard stops (instant Failed)

Treat as automatic **Failed** pre-check if the change would:

- Accept `company_id` / `tenant_id` from client body/query for isolation  
- Allow Product/Vendor INSERT without a DB transaction that also creates `subsidiary_accounts`  
- Allow Product without Category + Sub-Category + Brand + Unit  
- Soften or drop `retail_price >= invoice_price`  
- Soften NTN mask away from `^[0-9]{7}-[0-9]{1}$` (documents format; field optional)  
- Enable `ON DELETE CASCADE` from COA parents to system subsidiaries, or allow DELETE of `is_system_generated = 1` subsidiaries  
- Replace keyset `(company_id, id)` list APIs with deep `OFFSET` pagination on hot paths  
- Move notification/email/report generation into the synchronous request pipeline for catalog writes  

### 1.4 Status legend

| Status | Meaning |
|--------|---------|
| `[In-Progress]` | Active milestone; tasks may be partially complete |
| `[Pending Baseline]` | Locked until prior hard gates complete |
| `[Blocked]` | Waiting on dependency / exception |
| `[Done]` | Implemented + regression verified |

---

## 2. LOGICAL MODULE TASK MATRIX & CORE BOUNDARY CHECKS

---

### Module 0 — Identity, tenancy & security kernel

**Milestone status:** `[In-Progress]`

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **0.1** | JWT payload must bind tenant context after login | Token contains `user_id`, `tenant_id` (→ `company_id`), `role` | Middleware maps claims into `ITenantProvider`; EF filters use JWT tenant only | `[In-Progress]` |
| **0.2** | Reject client-submitted payload `company_id` / `tenant_id` for isolation | BRD §2 — never trust client for tenant boundary | API ignores/forbids body/query tenant ids for authz; only JWT | `[In-Progress]` |
| **0.3** | Password verify via bcrypt; min length 8 | BRD §4.1 | No plaintext storage; 422 on validation failure | `[Pending Baseline]` |
| **0.4** | Multi-company login requires explicit Company selection | User may belong to many tenants | Login fails without valid membership `companyId` | `[In-Progress]` |
| **0.5** | Audit columns stamped on all inserts/updates | `insert_date`, `insert_user_id`, `update_date`, `update_user_id` | DbContext enforcement; no silent null actor on authenticated writes | `[In-Progress]` |
| **0.6** | Super Admin ops written to central `audit_logs` | BRD §3.1 | Platform mutations append-only logged | `[Pending Baseline]` |
| **0.7** | User–tenant membership unique `(user_id, company_id)` | DB design M0 | Unique index enforced | `[Pending Baseline]` |
| **0.8** | Authorization middleware on every protected route | BRD §3.2 developer rule | Not frontend-only hiding | `[In-Progress]` |

**Module 0 gate to exit `[In-Progress]` → `[Done]`:** Real user store + bcrypt + tenant seed + 0.2 integration tests green.

---

### Module 1 — Chart of Accounts (HARD GATE)

**Milestone status:** `[Passed]`  
**Depends on:** M0 tenancy kernel  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **1.1** | Master accounts are platform-shared; **read-only for tenants** | BRD §3.2 / §4.7.1 | Tenant APIs: GET only; writes Super Admin only | `[Passed]` T1.61 |
| **1.2** | Force unique composite `(company_id, account_no)` on subsidiaries **and** global unique `account_no` | DB design §2.1.3 + T1.72 | Unique indexes `ux_sub_company_account_no` + `ux_sub_account_no` | `[Passed]` T1.72 |
| **1.3** | Unique `(company_id, subsidiary_account_code)` | DB design | Unique index required | `[Passed]` T1.63 |
| **1.4** | Hierarchy: Master → Control → Subsidiary; no floating subsidiaries | `control_account_id` NOT NULL + FK RESTRICT | Cannot insert subsidiary without Control | `[Passed]` T1.63 |
| **1.5** | Seed Control kinds `INVENTORY` and `ACCOUNTS_PAYABLE` | Automation targets | Filtered unique on `control_kind` for those two | `[Passed]` T1.62 |
| **1.6** | Block DELETE of `is_system_generated = 1` subsidiaries | BRD financial integrity | App + optional trigger; FK RESTRICT from products/vendors | `[Passed]` T1.63 / T2.18 (409) |
| **1.7** | `account_no` CHAR(12) + `account_code` system formats (immutable after create) | T1.76 coding SSoT | `SubsidiaryAccountCoding` — No=Tenant+seq; Code=Tenant-M/P/V/CASH/BANK | `[Passed]` T1.76 / T1.80 |
| **1.8** | COA list endpoints use keyset pagination | Performance baseline | `(company_id, id)` seek | `[Passed]` T2.18 Masters+Controls+Subsidiaries |

**Hard stop:** Do not start M3/M4 write paths until **1.5** seed is live.

---

### Module 2 — Catalog masters (HARD GATE for Product)

**Milestone status:** `[Passed]` *(M2 masters live; Product FK Unit deferred to M3)*  
**Depends on:** M0  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **2.1** | Category unique per tenant (case-insensitive) | `(company_id, name_normalized)` UNIQUE | BRD composite unique | `[Passed]` T2.1 |
| **2.2** | Category image required; JPG/JPEG/PNG; max 2MB | UI + BRD §4.2 | Client Zod + server content validation | `[Passed]` T2.2 |
| **2.3** | Sub-Category unique within parent Category | Unique `(category_id, name_normalized)` | Cascading dropdown only same-company categories | `[Passed]` T2.3 |
| **2.4** | Category delete RESTRICT if children/products exist | FK RESTRICT / soft-deactivate | No orphan sub-categories | `[Passed]` T2.4 |
| **2.5** | Brand unique within tenant | `(company_id, name_normalized)` | — | `[Passed]` T2.5 |
| **2.6** | Units system table; Product FK required | Pcs/Kg/Ltr/Box/Pack seed | Dropdown rejects raw text | `[Passed]` T2.6 *(Units seeded + GET; Product FK deferred to M3)* |
| **2.7** | Category/Sub codes stable for SKU segments | DB `code` columns | SKU generation reads codes, not free text only | `[Passed]` T2.7 |
| **2.8** | UI validation: onBlur / onChange clear / submit guard | `Xenasys_UI_Validation_Plan.md` | RHF + Zod; errors `#DC2626` | `[Passed]` T2.8 |

**Hard stop:** Do not enable Product POST until **2.1–2.7** exist.

---

### Module 3 — Products (SKU + inventory subsidiary)

**Milestone status:** `[Passed]`  
**Depends on:** M1 + M2  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **3.1** | **Atomic DB transaction** binding SKU generation + `subsidiary_accounts` insert + product link | BRD §4.5 / Trigger 1–2 | Single TX; rollback all on any failure | `[Passed]` T1.81 / T3.2 |
| **3.2** | Hard-enforce `Retail Price >= Invoice Price` | CHECK + Zod refine + API 422 | Cannot persist violating prices | `[Passed]` T3.1 |
| **3.3** | Product requires Category, Sub-Category, Brand, Unit | Data constraint gate | All FKs NOT NULL + same `company_id` | `[Passed]` T3.1 |
| **3.4** | Sub-Category must belong to selected Category | Cascading integrity | Server validates parent match | `[Passed]` T3.2 |
| **3.5** | Unique `(company_id, sku)` | DB design | Unique index | `[Passed]` T0.4 |
| **3.6** | Subsidiary under **Inventory Control** (`control_kind = INVENTORY`) | Title = Product Name only (SKU separate) | `is_system_generated = 1` | `[Passed]` T1.81 / T3.7 |
| **3.7** | Prices `decimal(10,2)` and `> 0` | BRD | CHECK constraints | `[Passed]` T3.1 |
| **3.8** | Keyset list `GET /api/products?afterId&limit` | No OFFSET hot path | Index `(company_id, id)` + TotalCount | `[Passed]` T3.2 |
| **3.9** | Active toggle may be optimistic UI but must hit authorized API | Performance plan | Still tenant-scoped | `[Passed]` T3.2 / T3.3 |
| **3.10** | Map HTTP 422 field errors under inputs | UI Validation Plan §4 | `mapApiErrorsToForm` / extractProductApiError | `[Passed]` T3.3 |

---

### Module 4 — Vendors (AP subsidiary)

**Milestone status:** `[Passed]`  
**Depends on:** M1 (may parallel M2/M3)  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **4.1** | Auto-initialize sub-ledger under **Accounts Payable** on save | BRD §4.6 / Trigger 3 | Same atomic TX pattern as Product | `[Passed]` T1.82 / T2.16 |
| **4.2** | Validate NTN mask `1234567-8` (optional field) | Regex `^[0-9]{7}-[0-9]{1}$` | Client Zod + server + unique `(company_id, ntn_no)` when set | `[Passed]` T2.15 |
| **4.3** | Vendor phone required; PK mobile `+92` + 10 digits | Stakeholder override of BRD loose regex | 422 on fail | `[Passed]` T2.13 / T2.16 |
| **4.4** | Subsidiary title `Vendor: [Vendor Name]` | BRD | `is_system_generated = 1` | `[Passed]` T1.82 |
| **4.5** | Keyset vendor lists; tenant filter from JWT | Performance | `(company_id, id)` | `[Passed]` T2.10 / T2.19 |
| **4.6** | Block delete of system AP subsidiary; vendor soft-deactivate allowed | COA integrity | FK RESTRICT + IsSystemGenerated 409 | `[Passed]` T2.19 |

---

### Module 5 — Stock & alerts

**Milestone status:** `[Passed]`  
**Depends on:** M3  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **5.1** | Stock updates reference existing Product only | FK RESTRICT | No stock without product | `[Passed]` T5.1 |
| **5.2** | When `quantity_on_hand <= minimum_stock_level`, enqueue alert | BRD min-stock | **Outbox async** — not sync email in HTTP | `[Passed]` T5.1 |
| **5.3** | Stock movements ledger optional but preferred at scale | DB design M5 | Index `(company_id, product_id, occurred_at)` | `[Passed]` T5.1 |
| **5.4** | `minimum_stock_level` integer ≥ 0 | BRD | CHECK + Zod | `[Passed]` T5.1 |

---

### Module 6 — Granular RBAC

**Milestone status:** `[Passed]`  
**Depends on:** M0; harden as M1–M4 routes appear  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **6.1** | Enforce BRD §3.2 matrix per module | Super / Tenant Admin / Operational | Middleware + permission codes | `[Passed]` T6.1 |
| **6.2** | Tenant Admin cannot access Super Admin dashboard | Matrix | 403 | `[Passed]` T6.1 |
| **6.3** | Operational roles CRUD configurable per module | Custom roles | No privilege escalation across tenants | `[Passed]` T6.1 |
| **6.4** | Master COA write denied to tenants | Aligns Task **1.1** | — | `[Passed]` T1.61 |

---

### Module 7 — Super Admin aggregation

**Milestone status:** `[Passed]`  
**Depends on:** M3–M5 data existence  

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **7.1** | Provide `vw_super_admin_inventory_summary` (or materialization) | BRD Trigger 4 | <1s via rollup preferred | `[Passed]` T7.1 |
| **7.2** | Provide `vw_super_admin_sales_by_tenant` (live via M8 Sales Orders) | BRD Trigger 4 | Do not fake live heavy scans | `[Passed]` T7.1 / T8.1 |
| **7.3** | Super Admin may use `IgnoreQueryFilters` only with explicit tenant selector + audit | Tenancy model | Never default to leaking all rows to tenant roles | `[Passed]` T7.1 |
| **7.4** | Cross-tenant APIs Super Admin only | Matrix | 403 otherwise | `[Passed]` T7.1 |

---

### Module 8 — Sales Orders (Phase-1 ledger)

**Milestone status:** `[Passed]`  
**Depends on:** M3 Products + M5 Stock + M7 view contract  
**Plan SSoT:** `Xenasys_Sales_Module_vs_Stub_Defer_Plan.md`

| Task ID | Description | Business rule baseline | Boundary check | Status |
|---------|-------------|------------------------|----------------|--------|
| **8.1** | Phase-1 Sales Orders header+lines CRUD; stock deduct/restore in TX | BRD Trigger 4 volumes; defer POS/checkout | No fabricated sales; soft cancel restores QOH | `[Passed]` T8.1 |
| **8.2** | Replace `vw_super_admin_sales_by_tenant` stub with Live aggregate | Trigger 4 | `Status=Live`; GrossSales/OrderCount from Posted active orders | `[Passed]` T8.1 |
| **8.3** | Claims `sales.*` + Form `/sales` + FE listing | §3.2 / Company gold | ClaimGate + FormRouteGate | `[Passed]` T8.1 |
| **8.4** | Defer Phase-2 (POS, portal checkout, invoices, returns, GL journals) | No §4 Sales form in BRD v1.1 | Documented only — no partial fake UI | `[Passed]` plan |

---

### Cross-cutting performance & UI integrity (applies to all modules)

| Task ID | Description | Boundary check | Status |
|---------|-------------|----------------|--------|
| **X.1** | Hot lists: keyset only; virtualize large grids | TanStack Virtual + `(company_id, id)` | `[In-Progress]` |
| **X.2** | No blocking sync loops / heavy jobs on request thread | Outbox / background for alerts & reports | `[Pending Baseline]` |
| **X.3** | Form UX: onBlur validate, onChange clear, submit guard | `Xenasys_UI_Validation_Plan.md` | `[Pending Baseline]` |
| **X.4** | Error chrome `#DC2626` for schema failures | Tailwind `text-red-600` / `border-red-600` | `[Pending Baseline]` |
| **X.5** | Master data query staleTime long (categories/tenant) | TanStack Query plan | `[In-Progress]` |

---

## 3. REGRESSION COMPLIANCE AUDIT LOG

### 3.1 Rules

1. **Every newly assigned task MUST be appended here BEFORE coding begins.**  
2. Fill `Pre-Check Conflict Status` using §1.2.  
3. Set `Regression Verified` to `Yes` only after: unit/integration checks for touched Task IDs + manual boundary smoke.  
4. If pre-check is `Failed`, do not code; escalate architectural exception.

### 3.2 Template (append rows downward)

| Task ID | Module | Description | Pre-Check Conflict Status (Passed/Failed) | Regression Verified (Yes/No) |
|---------|--------|-------------|---------------------------------------------|--------------------------------|
| CFG-01 | Workspace | Add `.cursorrules` (Clean Arch, async-only, ban OFFSET hot paths) | Passed | Yes |
| CFG-02 | Workspace | Add `Xenasys_API_Specification.md` (200/401/422 envelopes) | Passed | Yes |
| CFG-03 | Workspace | Add `Xenasys_Redis_Cache_Architecture.md` (tenant keys + invalidation) | Passed | Yes |
| CFG-04 | Workspace | Add `Xenasys_Testing_Sanity_Matrix.md` (ISO multi-tenant scenarios) | Passed | Yes |
| 0.9 | M0 Persistence | SQL Server DefaultConnection + EF InitialCreate migration wiring | Passed | Yes |
| 0.10 | M0+M1 Seed | Platform tenant/user/membership + COA master/control seed engine | Passed | Yes |
| T0.2 | M0+M1 Hotfix | Sequential/v7 seed IDs; Super Admin NULL CompanyId; InsertUserId = SuperAdmin | Passed | No |
| T0.4 | M0 Schema | INT IDENTITY PKs + CHAR(16) product_code composite; UNIQUE covering index | Passed | Yes |
| T0.3 | M0 Auth | Auth API routing + JWT engine (BCrypt verify, membership Include, claims) | Passed | Yes |
| M0.1 | M0 Tenants | tenant_code CHAR(6) unique + is_frozen + audit; keep INT IDENTITY PKs | Passed | Yes |
| M0.2 | M0 Auth | Multi-company login challenge list after BCrypt verify | Passed | Yes |
| M0.3 | M0 RBAC | SUPER_ADMIN / TENANT_ADMIN / TENANT_STAFF + claim mismatch middleware | Passed | Yes |
| M0.4 | M0 RBAC | permissions + role_permissions seed matrix (system-locked) | Passed | Yes |
| T0.5 | M0 Security | TenantClaimGuard single key company_id (remove ambiguous aliases) | Passed | Yes |
| T0.6 | Frontend | Yarn node-modules linker — fix Turbopack PnP resolution crash | Passed | Yes |
| T0.8 | Frontend | Split-screen login UI + company name→id login binding | Passed | Yes |
| T0.9 | M0 Schema | PascalCase columns: TenantCode/IsFrozen; CompanyNn→CompanyNo | Passed | Yes |
| T0.10 | Docs | Inline WHAT/WHY/EXAMPLE banners across M0 persistence/auth/login | Passed | No |
| T0.11 | Process | Activate Roman English task engine in .cursorrules + .cursor/rules | Passed | No |
| T0.12 | M0 Auth | TEMP: seed + verify passwords as plaintext for local login debugging | Passed | No |
| T0.13 | M0 UI | Login password field show/hide toggle | Passed | No |
| T0.14 | M0 UI | Fix login companies API URL (7115→5077 HTTP) | Passed | No |
| T0.15 | M0 UI | Remove unverified ISO 27001 / SSL trust badge copy | Passed | No |
| T0.16 | M0 UI | Align login fonts/size/theme to Login Page.png | Passed | No |
| T0.17 | M0 UI | Implement Main screen shell (sidebar+header+footerlogo) | Passed | No |
| T0.18 | M0 UI | Polish login + dashboard chrome to professional Main/Login mock | Passed | No |
| T0.19 | M0 QA | Micro QA via Playwright: password toggle, profile dup, footer, empty state | Passed | No |
| T0.20 | M0 UI | Sidebar redesign — top main logo + text footer (KENASYS Development) | Passed | No |
| T0.21 | M0 UI | Shared main logo on login; move KENASYS credit to main-screen footer | Passed | No |
| T0.22 | Process | BRD 11-point best sequence file + gap implementation wave | Passed | No |
| T0.23 | Process | Rewrite 11-point BRD-aligned sequence + implementation plan from BRD v1.1 | Passed | No |
| T0.24 | Process | Add simple wording + examples + verify steps for all 11 BRD points | Passed | No |
| T0.25 | M0 RBAC | Go-ahead wave: users/roles APIs+UI; plaintext passwords kept (no bcrypt restore) | Passed | No |
| T0.26 | M0 RBAC | newSalon-style Permission Management hub + audit/matrix APIs (Module×Action) | Passed | No |
| T1.0 | M0 UI | Force brand theme tokens; remove emerald; standardize inputs + alert wrappers | Passed | No |
| T1.1 | M0 UI | Lock Listing + Form drawer pattern from Category screens into docs/rules/tokens | Passed | No |
| T1.2 | M0 UI | Recreate Permission Management module (listing + right drawer) to Category pattern | Passed | No |
| T1.3 | M0 UI | Port newSalon DataTable (jspreadsheet-ce) to Permission listings; no loading delay flash | Passed | No |
| T1.4 | M0 UI | Rebuild User Management to T1.1 contract: 5 claim-gated tabs, dismissible alerts, virtualized perms, UPPERCASE role codes | Passed | No |
| T1.5 | M0 UI | Fluid full-width User Management canvas — remove max-w-6xl; responsive tabs/toolbar gutters | Passed | No |
| T1.6 | M0 RBAC | User ↔ Company mapping tab + POST /api/users/company-mapping (BRD §4.1 membership) | Passed | No |
| T1.7 | M0 UI | Consolidate Company/RBAC into SETTINGS sidebar; remove standalone USER MANAGEMENT section | Passed | No |
| T1.8 | M0 Settings | Full Settings module: Tenant.Address + company.read/update, PUT /api/tenant/me, /settings/* UI + redirects | Passed | No |
| T1.9 | M0 Company | Tenants CRUD API + Company Listing/drawer (Category pattern); Settings tabs removed; Settings sidebar dropdown | Passed | No |
| T1.10 | M0 Company | Listing viewport min-height; Actions View/Edit/Delete (Category SSoT); Tenant Admin company.delete scoped | Passed | No |
| T1.11 | M0 Search | Global Search Center: GET /api/search TOP-N + FE debounce/AbortController (no full-table preload) | Passed | No |
| T1.12 | M0 UI | Remove duplicate listing `#`/`rowNo` column; keep SpreadsheetGrid row-index gutter only | Passed | No |
| T1.13 | M0 Tenants | AuditClock PKT InsertDate; drop Tenants.ShortName+CompanyNo; keep TenantCode+IsFrozen (M0.1) | Passed | No |
| T1.14 | M0 Tenants | Drop IsFrozen (IsActive-only control); rebuild Tenants so audit cols are last | Passed | No |
| T1.15 | M0 Tenants | Auto TenantCode = namePrefix(3)+seq(3); PlatformSequences UPDLOCK; product_code uses TenantCode | Passed | No |
| T1.16 | M0 Perf | Active-only default lists + DataShape list/detail projections; IX_Tenants_IsActive_Name | Passed | No |
| T1.17 | M0 Tenants | TenantCode capacity Prefix2+Seq4 (max 9999); seed realistic production-volume companies | Passed | No |
| T1.18 | M0 Company | UI naming Company Code (not Tenant); Company Module Micro-Pin Playbook for other modules | Passed | No |
| T1.19 | M0 UI | DataTable header style + per-col filter/sort; fix empty filtered rows + sticky header overlap | Passed | No |
| T1.20 | M0 UI | Professional delete ConfirmDialog; stylish disabled/enabled form field chrome | Passed | No |
| T1.21 | M0 Docs | Complete Module Delivery Contract — Company gold standard for clone-next-module | Passed | No |
| T1.22 | M0 Roles | Complete Roles module (Company pattern): RoleCode Prefix2+Seq4, Description, IsActive, CRUD | Passed | No |
| T1.23 | M0 Roles | Rebuild Roles table — business cols first, audit last; backfill Description | Passed | No |
| T1.24 | M0 Roles | Roles columns lock: RoleCode+Name+Description+IsActive+audit; UI matches module fields | Passed | No |
| T1.25 | M0 Roles | Drop Roles.CompanyId + Roles.IsSystem; global RoleCode unique; system guard via RoleCode | Passed | No |
| T1.26 | M0 Actions | Company-style Actions CRUD (Name+IsActive); no claim gates yet; route /settings/actions | Passed | No |
| T1.27 | M0 Forms | Company-style Forms CRUD (Name, Path, IsActive + audit); nav catalog for PM | Passed | No |
| T1.28 | M0 Roles | BRD: restore Roles.CompanyId (company-scoped); column order business→audit | Passed | No |
| T1.29 | M0 Users | Company-style Users CRUD (create/view/edit + soft deactivate membership) | Passed | No |
| T1.30 | M0 Users | Remove User ↔ Company extra module (nav/UI/API mapping) | Passed | No |
| T1.31 | M0 Roles | Remove Form Mapping extra module; fold permissions into Roles drawer | Passed | No |
| T1.32 | M0 Auth UX | Header User/Role/Company + Profile update (name/password; roles locked) | Passed | No |
| T1.33 | M0 RBAC | Nav/API claim-gated by role permissions; forms.*/actions.* codes; custom roles work | Passed | No |
| T1.34 | M0 RBAC | Form×Action infra (FormActions + RoleFormActions); company via Role; hasForm/hasAction | Passed | No |
| T1.35 | M0 RBAC | Access Restricted popup on denied URL; per-action show/hide on Settings listings | Passed | No |
| T1.36 | M0 RBAC | Drop Permissions/RolePermissions tables; Form×Action-only SSoT (newSalon-clean) | Passed | No |
| T1.37 | M0 Users | User ↔ Company mapping screen (Form×Action); default TENANT_ADMIN grant | Rolled Back | No |
| T1.38 | M0 Users | Multi-role assign on Users module (UserRoles + PermissionGate union) | Passed | No |
| T1.39 | M0 Users | Users detail/list show company memberships for the user | Passed | No |
| T1.41 | M0 RBAC | Permission Audit = newSalon form→action→roles accordion (no redesign) | Passed | No |
| T1.42 | M0 Auth UX | Header User Name = live Users.UserName (remove Platform Admin / Demo Admin aliases) | Passed | No |
| T1.43 | M0 RBAC | Super Admin global visibility: all users + all roles (+ search/audit); companies already OK | Passed | No |
| T1.44 | M0 Roles | Company-wise Roles UX: Company column + SA create company_id + cross-company mutate | Passed | No |
| T1.45 | M0 Users | Super Admin create user with company_id + roles filtered by company (provision Tenant Admin) | Passed | No |
| T1.46 | M0 RBAC | Platform-only Settings (Company/Actions/Forms/Permission Audit) = Super Admin; strip TENANT_ADMIN grants | Passed | No |
| T1.47 | M0 Roles | Shared platform TENANT_ADMIN/STAFF (no per-company duplicates); consolidate + stop auto-create on Company register | Passed | No |
| T1.48 | M0 Roles | Retire TENANT_STAFF — platform TENANT_ADMIN only; remap staff users to TENANT_ADMIN | Passed | No |
| T1.49 | M0 RBAC | Fresh-admin reset: keep superadmin/tenantadmin + SUPER_ADMIN/TENANT_ADMIN; purge test Users/Roles | Passed | No |
| T1.50 | M0 Users | Remove seeded tenantadmin; Super Admin provisions Tenant Admin users only | Passed | No |
| T1.51 | M0 Users | Fix Create user 500: EF-translatable platform TENANT_ADMIN filter; reactivate + FE errors | Passed | No |
| T1.52 | M0 Grid UX | Server-side DataTable header text filters (Contains) on all list APIs + Products keyset | Passed | No |
| T1.53 | M0 Search | Global Search hit ?q= seeds listing columnFilters (company/users/roles/products) | Passed | No |
| T1.54 | M0 Brand | Collapsed sidebar orbital mark + favicon/apple-icon from brand mark | Passed | No |
| T1.55 | M0 RBAC Docs | Permission×BRD gap resolve plan (multi-company, header company profile, clarifications) | Passed | No |
| T1.56 | M0 Users | Super Admin multi-company membership assign from Users module (add/update/deactivate) | Passed | No |
| T1.57 | M0 Auth UX | Tenant Admin header My Company profile (JWT /api/tenant/me; /company-profile claims) | Passed | No |
| T1.58 | M0 Audit | PlatformAuditLogs central Super Admin ops trail + GET /api/platform-audit-logs | Passed | No |
| T1.60 | M0 Tenancy | BRD §2 exception: SA create company_id = provisioning; TA JWT-only (docs+guard note) | Passed | No |
| T1.61 | M1 Master | Master Account CRUD (platform-shared); SA write / tenant read-only; seed 5 masters; `/chart-of-accounts` UI | Passed | No |
| T1.62 | M1 Control | Control Account CRUD; INVENTORY/AP seed+unique; auto Code; SA write / tenant read-only; `/control-accounts` | Passed | No |
| T1.63 | M1 Subsidiary | Subsidiary CRUD (tenant-scoped); ACC-yyyy-seq; keyset list; block system delete; unique code/account_no | Passed | No |
| T1.64 | M1 Subsidiary | Super Admin list View-all companies (JWT company must not scope); Company column on grid | Passed | No |
| T1.65 | M1 Subsidiary | Per-company baseline Cash + Bank (separate) + Misc under OTHER control; BRD own-tenant sub-ledgers | Passed | No |
| T1.66 | M1 Subsidiary | Fix listing TotalCount (API LongCount); AccountNo sequence bootstrap past seed; ledger isolation notes | Passed | No |
| T1.67 | M1 Control | Product COA controls: INV+AP+Cash+Bank+Sales+COGS; Cash/Bank subsidiaries under Assets controls | Passed | No |
| T1.68 | M1 Control | Control Kind not on create/edit forms; View-only Type; create defaults OTHER | Passed | No |
| T1.69 | M1 Control | Type only in View details (no form select, no grid column); create→OTHER | Passed | No |
| T1.70 | M1 Control | View Type label: Automation (Product) for INV/AP; Other otherwise | Passed | No |
| T1.71 | M1 COA | Type/Source labels: Automation (Product|Vendor) vs Manual tag | Passed | No |
| T1.72 | M1 Subsidiary | AccountNo ACC-{TenantCode}-{yyyy}-{seq}; global unique UX; legacy rewrite | Passed | No |
| T1.73 | M1 Subsidiary | AccountNo CHAR(16)=TenantCode+yyyy+seq6; tighten column; rewrite hyphenated | Passed | No |
| T1.74 | M1 Subsidiary | AccountNo CHAR(12)=TenantCode+seq6 (drop year); lifetime seq per company | Passed | No |
| T1.75 | M1 Subsidiary | Baseline codes {TenantCode}-CASH/BANK (no shared OTH-CASH across companies) | Passed | No |
| T1.76 | M1 Subsidiary | Unified SubsidiaryAccountCoding; Code+No auto+immutable; FE locked fields | Passed | No |
| T1.77 | M1 COA Docs | Accounts modules BRD gap resolve plan (Master/Control/Subsidiary) | Passed | No |
| T1.78 | Docs | Remove superseded MD: BRD_11_Point sequence + Permission newSalon mapping | Passed | No |
| T1.79 | M1 Control | Tenant-owned Controls (CompanyId); TA Full Own; SA View-all; per-company INV/AP seed | Passed | No |
| T1.80 | M1 Subsidiary | AccountNo BRD ACC-{yyyy}-{seq5}; keep system Account Code immutable | Passed | No |
| T1.81 | M1/M3 | Product create TX → Inventory subsidiary (IsSystemGenerated) | Passed | No |
| T1.82 | M1/M4 | Vendor minimal CRUD + AP subsidiary TX (IsSystemGenerated) | Passed | No |
| T1.83 | M1 Subsidiary | AccountNo globally unique ACC-{yyyy}-{seq5} (no cross-company dups) | Passed | No |
| T1.84 | Docs | BRD v1.1 green=done Accounts/Vendor; yellow=next M2 Catalog (4.2–4.4) | Passed | No |
| T2.1 | M2 Category | Unique `(CompanyId, NameNormalized)`; auto Code CAT01; soft-delete | Passed | No |
| T2.2 | M2 Category | Mandatory JPG/JPEG/PNG ≤2MB; local wwwroot uploads; static files | Passed | No |
| T2.3 | M2 SubCategory | Unique within Category; cascade filter; optional image; SUB01 | Passed | No |
| T2.4 | M2 Category | Soft-deactivate children; block delete when active products linked | Passed | No |
| T2.5 | M2 Brand | Unique `(CompanyId, NameNormalized)`; brands.* claims + Form bridge | Passed | No |
| T2.6 | M2 Units | System Units seed Pcs/Kg/Ltr/Box/Pack; GET /api/units | Passed | No |
| T2.7 | M2 Codes | PlatformSequences CAT_CODE_/SUB_CODE_ per company | Passed | No |
| T2.8 | M2 UI | Category/SubCategory/Brand listing pages RHF+Zod #DC2626 | Passed | No |
| T2.9 | M2 Catalog | CompanyName on Vendor/Category/Sub/Brand list+detail; SA sees all / tenant scoped; realistic 1k seed | Passed | No |
| T2.10 | M2 Catalog | Keyset TotalCount (filter-aware) on Vendor/Category/Sub/Brand — Subsidiary Accounts pattern | Passed | No |
| T2.11 | M4 Vendor | NTN Pakistan-only `^[0-9]{11}$` for now; column MaxLength 32 for future international | Passed | No |
| T2.12 | M4 Vendor | Form required only Vendor Name + NTN No; Address/Phone/Email/Rep optional | Passed | No |
| T2.13 | M4 Vendor | Phone/Rep No format `+92` + 10 digits (e.g. +923368288338) | Passed | No |
| T2.14 | M4 Vendor | NTN optional; form order Name → Phone No → NTN; filtered unique NTN | Passed | No |
| T2.15 | M4 Vendor | NTN documents mask `1234567-8` restored; field remains optional | Passed | No |
| T2.16 | M4 Vendor | Name+Phone required; AP auto-heal; FE real API errors; Create TX via CreateExecutionStrategy (retry-safe) | Passed | No |
| T2.17 | M2 SubCategory | Remove listing Category filter dropdown (form parent Category unchanged) | Passed | No |
| T2.18 | M1 COA | Close Accounts polish: Prefix UX, 409 system-delete, Cash/Bank protect, Finance claims, Masters/Controls keyset, Vendor M4 matrix close | Passed | No |
| T2.19 | M4 Vendor | Close Module 4 BRD tasks 4.1–4.6 as Passed (AP TX, phone, title, keyset, delete protect) | Passed | No |
| T3.1 | M3 Product | Full Product CRUD: BrandId+UnitId FKs; mandatory catalog; PUT/DELETE; keyset TotalCount; Vendor-style FE; 1k seed | Passed | No |
| T3.2 | M3 Product | ProductsController Vendor-style full CRUD: CreateExecutionStrategy TX, INV auto-heal, FK validate, PUT/DELETE, TotalCount filters | Passed | No |
| T3.3 | M3 Product | FE ProductListingPage Vendor gold-standard: DataTable+SideDrawer CRUD, ClaimGate, soft-delete, FK cascade, keyset filters | Passed | No |
| T3.4 | M3 Product | Load-test seed ~1000 products (companies 1,3–4–5–6) + INV subsidiaries; smoke create/price-guard/soft-delete verified | Passed | No |
| T3.5 | FE UX | Replace plain form/filter selects with shared SearchableSelect (Products, SubCat, COA, Roles, Audit) | Passed | No |
| T3.6 | M3 Product | SA product reg catalog scoped by companyId (TGL Make Up visible); SUB_ACCNO catch-up; SA/TA/NU create verified @2053 | Passed | No |
| T3.7 | M3 Product | INV subsidiary title = Product Name only (drop ` - SKU`); backfill existing product subsidiaries | Passed | No |
| T5.1 | M5 Stock | QuantityOnHand + StockMovements ledger + NotificationOutbox; Stock API/UI `/stock`; low-stock enqueue | Passed | Yes |
| T7.1 | M7 Super Admin | vw_super_admin_* + /api/super/* SA-only; /super/dashboard; tenant selector audit; sales stub | Passed | Yes |
| T7.2 | M7 Dashboard UX | Single /dashboard for all; Company Name SearchableSelect; responsive stylish UI; /api/dashboard | Passed | No |
| T7.3 | M7 Dashboard Prod | Activity filter (hide 2k empty); server totals; low-stock queue; keyset; shell scroll; skeletons | Passed | No |
| T7.4 | M7 Dashboard UX | Plain-language labels, Find company, Clear filter, action cards, Needs attention + Update links | Passed | No |
| T7.5 | M7 Dashboard Table | DataCard+DataTable (jspreadsheet) header filters + sticky pagination; compact layout for visibility | Passed | No |
| T7.6 | M7 Dashboard TA | Tenant Admin view: company chip, Low-only toggle, stock DataTable filters/pagination, tenant quick nav | Passed | No |
| T6.1 | M6 RBAC Harden | Fix PermissionGate platform TA + custom roles; platform /settings/system; nav claims; dashboard.perm; SA/TA/ops smoke | Passed | No |
| T6.2 | M6 RBAC Scope | PermissionGate JWT-company only (no cross-company TENANT_ADMIN bleed); Users UI role list same | Passed | No |
| T6.3 | M6 Action Icons | ListingRowActions hide Edit/Delete without handler; Stock showEdit=canUpdate; FormRouteGate Form×Action only | Passed | No |
| T8.1 | M8 Sales Phase-1 | Sales Orders DB+API+FE; stock TX; live vw_super_admin_sales_by_tenant; SalesModuleLive; stub retired | Passed | No |
| T5.2 | M5 Stock UX | footerStickyToParent + SideDrawer onPrimary Save Stock (footer prop was ignored) | Passed | No |
| T5.3 | M5 Notify Center | Header bell Notification Center + ringtone on new low-stock; poll stock.alerts | Passed | No |
| T3.8 | M3 Product Image | Product ImagePath mandatory JPG/PNG ≤2MB multipart (Category clone) | Passed | No |
| T3.9 | M3 Barcode | Optional barcode; format alnum/hyphen/commas; tenant-unique tokens; empty → auto-gen from ProductCode | Passed | No |
| | | | | |

### 3.3 Architectural exception flag format (when Failed)

```markdown
### EXCEPTION FLAG
- Date:
- Actor:
- Proposed Task ID:
- Conflicting Task IDs:
- Conflict summary:
- Proposed resolution (requires architect approval):
- Decision: Approved / Rejected
```

---

## 4. FIRST TASK ASSIGNMENT READY STATE

**Current open milestone:** Module 0 `[In-Progress]`  

**Suggested next assignable tasks (after audit-log row):**

1. **0.3** — bcrypt + real credential verify  
2. **0.7** — memberships unique + seed tenant/user  
3. Then unlock **Module 1** COA migrations (**1.4 → 1.5 seed**) before any Product write work  

**Do not assign M3 write tasks until M1 seed + M2 catalog baselines are Done.**

---

## 5. Quick conflict cheat-sheet

| If you are changing… | You must re-verify Task IDs… |
|----------------------|------------------------------|
| Auth / JWT / tenant header | 0.1, 0.2, 0.8 |
| COA schema or delete behavior | 1.1–1.6, 3.6, 4.1, 4.6 |
| Category / brand / unit | 2.1–2.7, 3.3, 3.4 |
| Product create/update/price | 3.1–3.7, X.3 |
| Vendor create/NTN/phone | 4.1–4.4 |
| Stock / alerts | 5.1–5.2, X.2 |
| Permissions | 6.1–6.4, 0.8 |
| Super Admin reports | 7.1–7.4, 0.2 |

---

*End of Master Task Matrix. This file is the strict execution contract — no feature work without §1 gate + §3 log row.*
