# Xenasys — Sales Module vs Stub Defer (BRD-aligned)

**Status:** Locked disposition — **Implement Phase-1 Sales Orders** (replace stub)  
**BRD SSoT:** `Xenasys_Market_Place_BRD_v1.1.docx` (Trigger 4 / §3.1 transaction volumes)  
**Related:** Task Matrix **M8 / T8.1**; M7.2 stub superseded when Phase-1 ships  
**Date:** 2026-09-16

---

## 1. Decision (one line)

| Option | Disposition |
|--------|-------------|
| Keep `PendingSalesModule` stub forever | **Rejected** — BRD Super Admin must track real transaction volumes |
| Full marketplace Sales (POS / checkout / invoices / returns) | **Deferred** — no §4 Sales form fields in BRD v1.1 |
| **Phase-1 Sales Orders ledger** | **Implement now** — feeds `vw_super_admin_sales_by_tenant` with live GrossSales / OrderCount |

---

## 2. BRD evidence (what is in / not in)

| BRD signal | Implication |
|------------|-------------|
| Trigger 4 — `vw_super_admin_sales_by_tenant` | Aggregation contract is mandatory |
| Super Admin tracks overall transaction volumes | Need posted order totals per tenant — not fabricated zeros |
| §4 modules list Category → Product → Vendor → Stock … | **No** Order / Checkout / Invoice field list |
| Implementation Sequence M7.2 | Stub allowed **until** Sales module exists |

**Conclusion:** BRD authorizes a **minimal Sales Orders write path** so Trigger 4 is truthful. POS / portal checkout / FBR invoices / customer CRM wait for a future Sales BRD chapter.

---

## 3. Phase-1 scope (ship)

### 3.1 Module identity

| Item | Value |
|------|--------|
| UI name | **Sales Orders** |
| Route / Form path | `/sales` |
| Claims | `sales.create` / `sales.read` / `sales.update` / `sales.delete` |
| CRUD roles | Tenant Admin + Super Admin (full); Staff create/read/update (no delete by default template) |

### 3.2 Fields

**Header (`SalesOrders`)**

| Field | Type | Required | Notes |
|-------|------|----------|--------|
| Order Code | string | auto | `SO` + 6-digit seq per company |
| Order Date | datetime | system | `AuditClock` PKT on create |
| Customer Name | string(150) | optional | Walk-in / counter note |
| Notes | string(500) | optional | |
| Gross Amount | decimal(18,2) | system | Sum of line totals |
| Status | string | system | `Posted` \| `Cancelled` |
| IsActive | bool | soft-delete | Cancel clears stock |

**Lines (`SalesOrderLines`)**

| Field | Type | Required |
|-------|------|----------|
| ProductId | FK | yes |
| Product Code / Name | snapshot | system |
| Quantity | int > 0 | yes |
| Unit Price | decimal(18,2) | yes (default RetailPrice) |
| Line Total | decimal | system = Qty × UnitPrice |

### 3.3 Behaviors

1. **Create** — single DB transaction: allocate code → insert header+lines → deduct `QuantityOnHand` → `StockMovements` (negative delta, reason `Sale {OrderCode}`) → low-stock outbox same as Stock module.  
2. **Update** — header only (`CustomerName`, `Notes`) while `Posted` + `IsActive`; **no line edits** (stock integrity).  
3. **Delete** — soft cancel: restore QOH + positive stock movements + `Status=Cancelled` + `IsActive=false`.  
4. **List/Get** — keyset; `shape=list|detail`; active-only default; SA may pass `companyId`.  
5. **SA view** — replace stub SQL so GrossSales / OrderCount aggregate **Posted + IsActive** orders; `Status = Live`.  
6. **Flags** — `SalesModuleLive = true` on Dashboard / Super Admin DTOs.

### 3.4 Out of scope (explicit defer)

- Customer master / CRM  
- POS terminal UX / barcode scan  
- Public marketplace checkout cart  
- Tax / NTN / FBR e-invoice  
- Returns / credit notes / partial cancel of lines  
- GL posting to Sales / COGS control accounts (COA seed exists; journals deferred)

---

## 4. Delivery checklist (Company gold standard)

- [x] Plan + Task Matrix audit row **before** code  
- [x] DB entities + Fluent + migration + live aggregation view  
- [x] API `SalesOrdersController` + DTOs + claims  
- [x] Forms seed `/sales` + Form×Action + PermissionCatalog + bridge  
- [x] FE `/sales` DataTable + SideDrawer (lines) + ClaimGate + ConfirmDialog  
- [x] Nav INVENTORY section; `SalesModuleLive` flip  
- [x] Smoke: create order → stock down → SA sales-by-tenant Live → cancel restores stock  

---

## 5. Acceptance (Phase-1 done)

1. `GET /api/super/sales-by-tenant` returns rows with `status: "Live"` and real totals (or zeros for idle tenants) — **never** `PendingSalesModule`. ✅  
2. Tenant/SA with `sales.create` can post a multi-line order and see QOH decrease with a stock movement. ✅ (`SO000001`, QOH 50→48)  
3. Soft-delete cancels order and restores QOH. ✅ (48→50)  
4. UI listing matches Company/Brand pins (no `#` column; sticky pagination; drawer View/Create/Edit). ✅  

---

*End of Sales vs stub defer plan. Phase-2 requires a dedicated Sales BRD chapter before expansion.*
