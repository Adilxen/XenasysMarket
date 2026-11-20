# Xenasys — Company Module Micro-Pin Playbook (Module SSoT)

**Status:** Baselined from Company Module implementation (T1.8–T1.20)  
**Audience:** AI agents + developers building the next Settings / catalog modules  
**Rule:** Copy this checklist **module-by-module**. Do not invent parallel UX/API patterns.  
**Companion:** `Xenasys_Complete_Module_Delivery_Contract.md` — user shares module name + min details → full DB/API/FE delivery.

> **Language:** UI + user-facing copy = **Company**.  
> **Wire / DB / JWT:** `tenant_id` → `company_id`, column/property `TenantCode` (internal).  
> Never show the word **Tenant** on screens, grids, drawers, or toasts.

---

## 0. Naming contract (resolve conflicts first)

| Layer | Term | Example |
|-------|------|---------|
| Sidebar / page title / buttons | **Company** | `Company`, `Add Company`, `Register Company` |
| Grid / form labels | **Company Name**, **Company Code** | Not “Tenant Code” |
| Search group label | **Companies** | Global Search Center |
| JSON / C# / SQL | `tenantCode` / `TenantCode` / `Tenants.TenantCode` | Keep for BRD + JWT alignment |
| JWT claim | `tenant_id` maps to `company_id` | Never accept `company_id` from client for isolation |

**Micro-pin:** If a column key is `tenantCode`, the **visible label must still say Company Code**.

---

## 1. Screen shell & layout

| Pin | Rule |
|-----|------|
| 1.1 | Route: `/settings/company` under Settings sidebar dropdown (no horizontal Settings tabs). |
| 1.2 | Page title: **Company** only — `breadcrumb={null}` (no “Settings / Company” chrome duplication). |
| 1.3 | Viewport lock: `TenantShell` `h-screen overflow-hidden`; listing uses `fill` + `DataCard fill` + `DataTable footerStickyToParent`. |
| 1.4 | Header + title + table + pagination + DEVELOPED BY footer fit **without page scroll**; table body scrolls inside card. |
| 1.5 | No per-screen search/filter toolbar — global **Search Center** (Cmd/Ctrl+K) only. |
| 1.6 | Thin global scrollbars in `globals.css`. |

**Reference components:**  
`settings-module-shell.tsx` → `SettingsPageChrome`  
`company-listing-page.tsx`  
`tenant-shell.tsx`

---

## 2. Listing (Category pattern)

| Pin | Rule |
|-----|------|
| 2.1 | `DataTable` spreadsheet variant (jspreadsheet-ce) — same as Category Listing. |
| 2.2 | **Do not** add a manual `#` / `rowNo` column — grid already has a row-index gutter (duplicate `#` bug). |
| 2.3 | List shape columns (minimal): Company Name, **Company Code**, Email, Status (+ Actions). |
| 2.4 | Status badge: Active → brand blue `#2563EB` / `#EFF6FF`; Inactive → muted gray. |
| 2.5 | Actions: View (eye) / Edit (pencil) / Delete (trash) via `ListingRowActions` — Category SSoT. |
| 2.6 | Pagination footer sticky in card: Total, Per page, Previous/Next. |
| 2.7 | Delete = soft deactivate (`IsActive = false`), **`ConfirmDialog`** (never `window.confirm`), then row disappears from default list. |
| 2.8 | DataTable: Category-style header chrome; per-column filter funnel = **server Contains** (debounced query params, not DOM hide); header asc/desc sort arrows (client on loaded sheet). |
| 2.9 | Form fields: stylish enabled vs disabled/read-only chrome via `inputClass` / `fieldLabelMutedClass` (View mode). |

---

## 3. Drawer CRUD

| Pin | Rule |
|-----|------|
| 3.1 | Right `SideDrawer`: create / view / edit modes. |
| 3.2 | RHF + Zod, `mode: "onBlur"`, errors `#DC2626`. |
| 3.3 | Create fields: Company Name (required), Contact Email/Phone, Address. |
| 3.4 | **Company Code is never user-entered** — placeholder “Auto-generated on save”; read-only after create. |
| 3.5 | View/Edit: fetch `GET /api/tenants/{id}?shape=detail` so Address/Phone load (list shape is slim). |
| 3.6 | Primary CTA: Register Company / Save Company; view mode = Close only. |
| 3.7 | `ClaimGate`: Add button only if `company.create` (Super Admin). |

---

## 4. Permissions & roles

| Claim | Who | UI / API |
|-------|-----|----------|
| `company.read` | Tenant Admin+, Super Admin | List / view |
| `company.create` | **Super Admin only** | Add Company + `POST /api/tenants` |
| `company.update` | Tenant Admin (own), Super Admin | Edit |
| `company.delete` | Tenant Admin (own), Super Admin | Soft deactivate |

| Pin | Rule |
|-----|------|
| 4.1 | Unauthorized actions **absent from DOM** (`ClaimGate`) — not merely disabled. |
| 4.2 | Demo Admin (`tenantadmin`) will **not** see Add — by design. |
| 4.3 | Seed/login: `superadmin` / `SuperAdmin@12345`, `tenantadmin` / `TenantAdmin@12345`. |

---

## 5. API contract

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/tenants` | Default: **active only**, `shape=list` |
| GET | `/api/tenants/{id}` | Default: `shape=detail` |
| POST | `/api/tenants` | Super Admin; allocates Company Code server-side |
| PUT | `/api/tenants/{id}` | Update profile; Super Admin may set `isActive` |
| DELETE | `/api/tenants/{id}` | Soft deactivate |
| GET/PUT | `/api/tenant/me` | Current JWT company profile |

### Query options (every hot list module must copy)

| Query | Default | Purpose |
|-------|---------|---------|
| `includeInactive` | `false` | Production 1CR safety — inactive never on hot path |
| `shape` | `list` on collections; `detail` on get-by-id | Column projection (SP-style select list) |
| column Contains | omit when empty | e.g. `name`, `tenantCode` — header funnel text → EF `ToLower().Contains` (T1.52) |

**Shapes:**

- `list` → Id, Name, TenantCode, IsActive, ContactEmail (grid only)  
- `detail` → + ContactPhone, Address (no audit columns on wire)

**Shared code:**  
`DataShape` / `DataShapeParser`  
`ActiveQueryExtensions.WhereActive`  
`TenantProjections` / `ProductProjections`

---

## 6. Auto Company Code (TenantCode)

| Pin | Rule |
|-----|------|
| 6.1 | Formula: **`[NamePrefix 2] + [Sequence 4]`** = CHAR(6). Example: Unilever → `UN0053`. |
| 6.2 | Capacity: **9,999** companies. |
| 6.3 | Atomic via `PlatformSequences` key `TENANT_CODE` + `UPDLOCK` (O(1), no Tenants scan). |
| 6.4 | Safety: `UX_Tenants_TenantCode` + allocate retry on rare UNIQUE race. |
| 6.5 | Clients never send `tenantCode` on create. |
| 6.6 | Seeded platform row: `MASTER` (legacy); new rows use prefix+seq. |

**Product code (ready for catalog):**  
`TenantCode(6) + UserNn(2) + Seq(8)` = CHAR(16).

---

## 7. Data model (Tenants table)

**Business columns (first):**  
`Id`, `Name`, `TenantCode`, `IsActive`, `ContactEmail`, `ContactPhone`, `Address`

**Audit columns (always last):**  
`InsertDate`, `InsertUserId`, `UpdateDate`, `UpdateUserId`

| Pin | Rule |
|-----|------|
| 7.1 | **No** `ShortName`, `CompanyNo`, `IsFrozen` — removed; control = `IsActive` only. |
| 7.2 | `InsertDate` / `UpdateDate` use **Pakistan Standard Time** (`AuditClock`) — not UTC. |
| 7.3 | Indexes: `UX_Tenants_TenantCode`; `IX_Tenants_IsActive_Name` (INCLUDE TenantCode, ContactEmail). |

---

## 8. Search Center

| Pin | Rule |
|-----|------|
| 8.1 | Companies group: active only; hit projection Id/Name/TenantCode only. |
| 8.2 | Sequential EF queries (never `Task.WhenAll` on one scoped `DbContext`). |
| 8.3 | TOP-N + debounce + AbortController on FE. |

---

## 9. Performance / production posture

| Pin | Rule |
|-----|------|
| 9.1 | Default lists = active + `shape=list` — mandatory for every module at 1CR scale. |
| 9.2 | Never `SELECT *` / full entity on list; always `Select(Projection)`. |
| 9.3 | Prefer keyset pagination on high-volume tables (Products already); Company list may add keyset when counts grow past UI comfort. |
| 9.4 | Filtered/covering indexes aligned to `WHERE IsActive = 1` + sort/seek columns. |

---

## 10. FE API helpers

```ts
listTenants({ shape: "list" })           // default
getTenant(id, "detail")                  // drawer
createTenant({ name, contactEmail, … }) // no tenantCode
```

---

## 11. Checklist — start any new module with this

1. [ ] UI vocabulary locked (no internal DB jargon on labels).  
2. [ ] Settings/sidebar nav + Category listing shell (`fill`, sticky pagination).  
3. [ ] No duplicate `#` column; no local search bar.  
4. [ ] Claims: create/read/update/delete gated; Super-Admin-only creates documented.  
5. [ ] Auto codes via `PlatformSequences` if applicable.  
6. [ ] `includeInactive=false` + `shape=list|detail` on every list/detail API.  
7. [ ] Projections + active indexes.  
8. [ ] Audit columns physically last in SQL.  
9. [ ] `AuditClock` (PKT) for Insert/Update dates.  
10. [ ] Task Matrix audit-log row before coding; append Passed row when done.  
11. [ ] Roman English replies to user; English-only source + WHAT/WHY/EXAMPLE banners.

---

## 12. Known test data (local)

- Active companies ≈ **2,051** (realistic Pakistan marketplace names + cities).  
- Super Admin sees all; Tenant Admin sees own company only.  
- Login companies endpoint also filters `IsActive = 1`.

---

## 13. Do-not-regress list

- Do not reintroduce `IsFrozen` / `ShortName` / `CompanyNo` on Tenants.  
- Do not show **Tenant** in UI labels.  
- Do not load inactive rows on default Company list.  
- Do not put `rowNo` / `#` data column beside spreadsheet gutter.  
- Do not run parallel EF queries on one scoped DbContext.

---

*Next modules: use `Xenasys_Complete_Module_Delivery_Contract.md` §B (user min details) + clone pins 1–11. Only swap entity projections, claims, and auto-code formula as needed.*
