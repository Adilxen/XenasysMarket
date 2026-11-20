# Xenasys — Complete Module Delivery Contract

**Gold standard:** Company Module (Settings → Company)  
**Playbook SSoT:** `Xenasys_Company_Module_Micro_Pin_Playbook.md`  
**Status:** Locked for clone-next-module workflow (T1.8–T1.20)

> **User workflow going forward:**  
> Share only **module name** + **minimum details** (fields, who can CRUD, any special codes).  
> Agent delivers **Database + API + Frontend** end-to-end using this contract — no partial UI-only / API-only drops unless asked.

---

## A. Company Module — what is DONE (baseline inventory)

### A.1 Related Task Matrix rows

| Task | Scope |
|------|--------|
| T1.7 | Company under SETTINGS sidebar (not separate User Mgmt section) |
| T1.8 | Settings shell; `company.read/update`; `/settings/*` |
| T1.9 | Tenants CRUD API + Company listing/drawer (Category pattern) |
| T1.10 | Viewport listing; View/Edit/Delete; scoped `company.delete` |
| T1.11 | Global Search Center (Companies group) |
| T1.12 | No duplicate `#` / `rowNo` column |
| T1.13–T1.14 | Schema cleanup: drop ShortName/CompanyNo/IsFrozen; audit cols last; PKT AuditClock |
| T1.15–T1.17 | Auto Company Code Prefix2+Seq4; PlatformSequences; seed volume |
| T1.16 | Active-only + `shape=list\|detail` projections + covering index |
| T1.18 | UI naming **Company Code**; Micro-Pin Playbook |
| T1.19 | DataTable header + column filter + asc/desc sort |
| T1.52 | Header funnel = server `Contains` query params (not client DOM hide); Products keyset resets cursor on filter change |
| T1.20 | Professional `ConfirmDialog` delete; disabled/enabled form chrome |

### A.2 Database (EF + SQL Server)

| Item | Detail |
|------|--------|
| Table | `Tenants` (UI name: Company) |
| Business cols | `Id`, `Name`, `TenantCode`, `IsActive`, `ContactEmail`, `ContactPhone`, `Address` |
| Audit cols (last) | `InsertDate`, `InsertUserId`, `UpdateDate`, `UpdateUserId` |
| Removed | `ShortName`, `CompanyNo`, `IsFrozen` |
| Unique | `UX_Tenants_TenantCode` |
| Hot index | `IX_Tenants_IsActive_Name` (INCLUDE TenantCode, ContactEmail) |
| Sequence | `PlatformSequences` key `TENANT_CODE` — UPDLOCK allocate |
| Code formula | NamePrefix(2) + Seq(4) → CHAR(6), max 9999 |
| Clock | `AuditClock` Pakistan Standard Time |
| Persistence style | **EF Core LINQ** (not Stored Procedures). Rare raw SQL only for sequence UPDATE. |

### A.3 API

| Surface | Notes |
|---------|--------|
| `GET/POST/PUT/DELETE /api/tenants` | CRUD; soft delete = `IsActive=false` |
| `GET/PUT /api/tenant/me` | Current JWT company profile |
| Defaults | `includeInactive=false`, list `shape=list`, get-by-id `shape=detail` |
| Claims | `company.read/create/update/delete` |
| Create | Super Admin only; code allocated server-side |
| Isolation | JWT `tenant_id` → `ITenantProvider`; never trust client `company_id` |
| Search | `GET /api/search` — Companies hits active-only, sequential EF |

**Key files:**  
`TenantsController.cs`, `TenantController.cs`, `TenantCodeAllocator.cs`, `TenantProjections.cs`, `ApplicationDbContext.cs`

### A.4 Frontend

| Surface | Notes |
|---------|--------|
| Route | `/settings/company` |
| Shell | `SettingsPageChrome` + viewport lock (`fill`) |
| Listing | `DataCard fill` + `DataTable` spreadsheet + sticky pagination |
| Columns | Company Name, Company Code, Email, Status + Actions (no `#`) |
| Grid UX | Header style; per-column filter funnel (server Contains); asc/desc sort arrows |
| Drawer | Create / View / Edit — RHF + Zod; View = disabled fields |
| Delete | `ConfirmDialog` (not `window.confirm`) |
| Claims | `ClaimGate` hides unauthorized CTAs |
| Search | Global Search Center only (no local toolbar search) |

**Key files:**  
`company-listing-page.tsx`, `confirm-dialog.tsx`, `data-table.tsx`, `spreadsheet-grid/*`, `field-styles.ts`

### A.5 Naming contract (locked)

| UI | Wire/DB/JWT |
|----|-------------|
| Company / Company Name / Company Code | `tenantCode` / `TenantCode` / `tenant_id`→`company_id` |

Never show **Tenant** on screens.

---

## B. What user must share for the NEXT module (minimum)

Send **only**:

1. **Module name** (UI label) — e.g. `Category`, `Brand`, `Vendor`  
2. **Fields** (name + type + required?) — short list  
3. **Who can CRUD** — Super Admin / Tenant Admin / both (or claim names if known)  
4. **Specials** (optional) — auto-code formula, soft-delete vs hard, parent FK, unique rules  

Example prompt:

```text
Module: Brand
Fields: Name (req), Description, IsActive
CRUD: Tenant Admin read/update/create/delete; Super Admin all
Auto code: optional BrandCode Prefix2+Seq4
```

Agent then builds full stack without waiting for screen-by-screen instructions.

---

## C. Agent delivery checklist (every new module)

Copy Company pins; swap entity only.

### C.1 Database
- [ ] Entity + Fluent config (PascalCase SQL)  
- [ ] Business columns first; audit last  
- [ ] `IsActive` soft-delete (unless hard-delete explicitly required)  
- [ ] Unique / FK / covering index for active list  
- [ ] Migration + Task Matrix audit row  
- [ ] Auto-code via `PlatformSequences` if required  
- [ ] Seed enough rows for realistic listing test  

### C.2 API
- [ ] Controller under correct route prefix  
- [ ] Claims: `{module}.read|create|update|delete`  
- [ ] `WhereActive` + `shape=list|detail` projections  
- [ ] JWT tenant isolation on tenant-scoped entities  
- [ ] Soft DELETE → `IsActive=false`  
- [ ] Search Center group if entity is searchable  
- [ ] No SP required (EF LINQ unless concurrency needs raw SQL)  

### C.3 Frontend
- [ ] Sidebar/nav entry + route  
- [ ] `SettingsPageChrome` or Catalog shell + `fill` listing  
- [ ] `DataTable` columns (no `#`); filters + sort enabled  
- [ ] `SideDrawer` create/view/edit; RHF+Zod; disabled View chrome  
- [ ] `ListingRowActions` + `ConfirmDialog` on delete  
- [ ] `ClaimGate` on Add / privileged actions  
- [ ] UI labels use business names (never leak DB jargon)  
- [ ] FE API helpers mirror Company (`listX`, `getX`, `createX`, …)  

### C.4 Quality gates
- [ ] `<1s` click targets; async EF/HTTP  
- [ ] Roman English user replies; English code + WHAT/WHY/EXAMPLE banners  
- [ ] Task Matrix row Passed when done  
- [ ] Do-not-regress: no emerald, no `IsFrozen` pattern, no parallel DbContext queries  

---

## D. Reference file map (Company)

```
Database / Persistence
  Entities/Tenant.cs
  Configurations/TenantConfiguration.cs
  Coding/TenantCodeAllocator.cs
  Querying/TenantProjections.cs
  Data/ApplicationDbContext.cs
  Migrations/*Tenants*

API
  Controllers/TenantsController.cs
  Controllers/TenantController.cs
  Controllers/SearchController.cs (Companies branch)

Frontend
  components/settings/company-listing-page.tsx
  components/settings/settings-module-shell.tsx
  components/ui/confirm-dialog.tsx
  components/ui/data-table.tsx
  lib/api/endpoints.ts (tenants)
  lib/forms/field-styles.ts
```

---

## E. Persistence note (for architects)

Current Xenasys API uses **EF Core**, not Stored Procedures.  
If a future module **must** use SPs, call that out in the minimum details; otherwise default = Company pattern (LINQ + projections).

---

*This contract + Company Micro-Pin Playbook are the clone template. Prefer updating these docs when Company gains a new pin rather than inventing a parallel pattern.*

---

## F. Modules delivered from this contract

| Module | Task | Notes |
|--------|------|--------|
| Company | T1.8–T1.21 | Gold standard |
| Roles | T1.22 | Role Code Prefix2+Seq4; Description; IsActive; CRUD claims create/update/delete; Company-pattern UI |
