# Accounts Modules (COA) × BRD Gap — Resolve Plan

**Source:** `Xenasys_Market_Place_BRD_v1.1.docx` (§3.2, §4.5–4.7, §5)  
**Scope:** Master Accounts · Control Accounts · Subsidiary Accounts only  
**Code baseline:** T1.61–T1.76 (platform Masters/Controls; tenant Subsidiaries; `SubsidiaryAccountCoding`)  
**Created:** 2026-09-16  
**Task Matrix:** T1.77 (this plan) → implementation IDs as each item starts  

**Related:** `Xenasys_Permission_BRD_Gap_Resolve_Plan.md` (RBAC wave) · `Xenasys_Task_Matrix.md` Module 1  

---

## How to use this file

1. Items marked **DO NOW** → implement one-by-one in resolve order.
2. Items marked **SKIP (stakeholder)** → no code until Approved.
3. Items marked **CLARIFY** → recommended answer + decision checkbox; code only after **Approved**.
4. After each shipped item: Status = Done + Task Matrix audit row.

### Resolve order (recommended)

| Wave | Item | Why this order |
|------|------|----------------|
| **1** | **#C1** Control tenancy / RBAC vs BRD §3.2 | **Approved 2026-09-16: tenant-owned Controls** |
| **2** | **#C2 / #C3** Account Code + Account No | **Approved: system Code immutable; Account No `ACC-{yyyy}-{seq5}`** |
| **3** | **#D1–#D3** Product + Vendor COA automation | **Approved: A+B+C this wave** |
| **4** | Masters naming | **Approved: keep Assets/Equity/Liabilities/Income/Expense** |
| **Later** | #D5 Finance claims · #C6 tree UI · polish | After Critical/High locked |

### Stakeholder decisions (2026-09-16)

| ID | Decision |
|----|----------|
| #C1 | **B** — Har company ke apne Control Accounts (BRD literal) |
| #C2 | **A** — System auto Account Code + immutable |
| #C3 | **B** — BRD style `ACC-2026-00981` |
| #C5 | **A** — Keep current master seed names |
| Scope | **A+B+C** — COA gaps + Product COA TX + Vendor COA TX |

---

## Status legend

| Status | Meaning |
|--------|---------|
| Pending | Not started |
| Ready | Clarified / plan locked — can code |
| Done | Shipped + Task Matrix row |
| Skipped | Stakeholder deferred |

---

## A. BRD requirements (3 modules)

### §3.2 Permission matrix (COA)

| Module | Super Admin | Tenant Admin | Operational |
|--------|-------------|--------------|-------------|
| Master Account Structure | Full Control (Global) | Read Only | Read Only |
| Control & Subsidiary Accounts | View All Tenants | Full Control (Own Tenant) | Configurable (Finance Roles) |

BRD note: Master = platform shared template. Control/Subsidiary = auto-created on Product/Vendor save.

### §4.7 Hierarchy

Master Account → Control Account → Subsidiary Account (strict; no floating subsidiaries).

### §4.7.1 Master Account

| Field | Rule |
|-------|------|
| Master Account Code | Mandatory; “Primary Key”; Auto/Manual (e.g. `1000`) |
| Master Account Title | Mandatory; max 100 |
| Prefix | Mandatory; max 5; prefix for child control codes |
| Active | Default checked |
| Audit | insert/update user + date |

### §4.7.2 Control Account

| Field | Rule |
|-------|------|
| Control Account Code | System Generated, Read-Only (e.g. `AST-CTL-001` from Master Prefix) |
| Master Account | Mandatory FK |
| Control Account Title | Mandatory; max 100 |

### §4.7.3 Subsidiary Account

| Field | Rule |
|-------|------|
| Subsidiary Account Code | **Text Input**; Mandatory; unique within tenant |
| Control Account | Mandatory parent |
| Subsidiary Account Title | Mandatory; max 150 |
| Account No | System Generated (example `ACC-2026-00981`) |
| Delete | Manual delete of Product/Vendor-linked subsidiaries **STRICTLY PROHIBITED** |

### §5 automation (COA contracts)

| Trigger | Action |
|---------|--------|
| Product save | Insert subsidiary under **Inventory Control**; title = Product Name only |
| Vendor save | Insert subsidiary under **Accounts Payable Control**; title `Vendor: [Vendor Name]` |

---

## B. Current implementation snapshot

| Area | Master | Control | Subsidiary |
|------|--------|---------|------------|
| Tenancy | Platform (no `CompanyId`) | Platform (no `CompanyId`) | Per `CompanyId` |
| Writes | Super Admin only | Super Admin only | TA own company; SA list View-all |
| Tenant read | Yes (`coa.masters.read`) | Yes (`coa.controls.read`) | Full TA CRUD claims |
| Code | Manual numeric unique | Auto `{Prefix}-CTL-…` | System `{Tenant}-M#####` / `-CASH`/`-BANK` (immutable) |
| Account No | — | — | `CHAR(12)` = TenantCode + seq6 (immutable) |
| Seed | 5 masters (Assets…Expense) | INV, AP, Cash, Bank, Sales, COGS | Per-company Cash + Bank |
| Kind | — | INV / AP unique; OTHER | — |
| System delete lock | Soft deactivate | Soft; INV/AP protected | `IsSystemGenerated` blocks delete (flag unused until automation) |
| UI routes | `/chart-of-accounts` | `/control-accounts` | `/subsidiary-accounts` |
| Pagination | Client full list | Client full list | Keyset + TotalCount |

---

## C. Mismatches (BRD ≠ code)

| ID | Topic | BRD | Code today | Priority | Status | Disposition |
|----|-------|-----|------------|----------|--------|-------------|
| **#C1** | Control ownership / RBAC | §3.2 TA Full Control (Own Tenant); SA View All | Platform Controls; TA **read-only**; SA writes | **Critical** | **Done** | Tenant-owned Controls |
| **#C2** | Subsidiary Account Code | §4.7.3 Text Input, unique per tenant | System-allocated + immutable (T1.76) | **High** | **Done** | Keep system immutable |
| **#C3** | Account No format | Example `ACC-2026-00981` | `CHAR(12)` TenantCode+seq (T1.74) | **High** | **Done** | BRD `ACC-{yyyy}-{seq5}` |
| **#C4** | Master Code as PK | Code called “Primary Key” | INT IDENTITY PK; Code unique alternate | **Medium** | **Skipped** | SKIP (DB design OK) |
| **#C5** | Master naming | Revenue / Expenses examples | Income / Expense + Equity | **Medium** | **Done** | Keep current seed names (approved) |
| **#C6** | COA UI topology | Impl Seq `/finance/coa` tree | Three flat routes; no tree | **Medium** | **Done** | `/finance/coa` tree + flat CRUD |
| **#C7** | System-delete HTTP status | Testing matrix **409** | Soft-delete block → **422** | **Medium** | **Done** | Conflict 409 |
| **#C8** | Master Prefix uniqueness | Implied by prefix-based control codes | Unique in API only; **no DB unique index** | **Medium** | **Done** | UX_MasterAccounts_Prefix |
| **#C9** | Control Code pattern | `AST-CTL-001` numeric seq | `AST-CTL-INV` / `…-CTL-0001` | **Medium** | **Skipped** | SKIP unless stakeholder wants numeric-only |

---

## D. Missing (BRD requires / not built)

| ID | Missing | BRD ref | Priority | Status | Disposition |
|----|---------|---------|----------|--------|-------------|
| **#D1** | Product save → Inventory Control subsidiary TX; title = Product Name only | §4.5 / §5 | **Critical** | **Done** | T1.81 / T3.7 |
| **#D2** | Vendor save → AP Control subsidiary TX; title `Vendor: [Vendor Name]` | §4.6 / §5 | **Critical** | **Done** | T1.82 |
| **#D3** | Allocator Product/Vendor kinds + set `IsSystemGenerated=true` | §4.7.3 delete rule + coding SSoT | **Critical** | **Done** | With #D1/#D2 |
| **#D4** | Tenant-owned Control CRUD (if #C1 chooses BRD matrix literal) | §3.2 | **Critical** | **Done** | T1.79 |
| **#D5** | Finance operational role claims for Control/Subsidiary beyond TA defaults | §3.2 Configurable | **High** | **Done** | Platform FINANCE role + Form×Action |
| **#D6** | Keyset pagination on Masters + Controls lists | Task Matrix **1.8** | **Medium** | **Done** | T2.18 |
| **#D7** | Protect baseline Cash/Bank from casual soft-delete (`IsSystemGenerated` or equivalent) | Integrity (not BRD text) | **Medium** | **Done** | Backfill + create-time flag |

---

## E. Clarifications (decide before coding)

### #C1 — Control Accounts: platform vs tenant-owned

**Question:** BRD §3.2 says Tenant Admin has Full Control on Control Accounts (Own Tenant). Code treats Controls as platform-shared (like Masters), SA write / TA read.

**Recommended:** Keep **platform Controls** (Task 1.1 / T1.62) so INV/AP automation targets are single global nodes; document as **architectural exception** to §3.2 Control row. Subsidiaries remain tenant Full Control (already matches BRD).

- [ ] **Approved — platform Controls (exception documented)**
- [ ] **Approved — change to tenant-owned Controls (#D4)**
- [ ] Deferred

**If platform approved:** update BRD gap note in Task Matrix exception flag; no Control tenancy rewrite.

**If tenant-owned approved:** design per-company Control rows + seed INV/AP per company + change claims — large wave before Product/Vendor.

---

### #C2 — Subsidiary Account Code: manual vs system

**Question:** BRD §4.7.3 = Text Input. T1.76 = system `{Tenant}-M#####` / `-CASH`/`-BANK`, immutable.

**Recommended:** Keep **system Account Code** (same sequence context as Account No); treat BRD “Text Input” as superseded by T1.76 coding SSoT.

- [ ] **Approved — keep system immutable Account Code**
- [ ] **Approved — restore manual text Code on create (Account No stays system)**
- [ ] Deferred

---

### #C3 — Account No format

**Question:** BRD example `ACC-2026-00981` vs live `CHAR(12)` `ACM007000042`.

**Recommended:** Keep **CHAR(12) TenantCode+seq** (cross-company unique, max 12); update BRD/docs wording only.

- [ ] **Approved — keep CHAR(12) Tenant+seq**
- [ ] **Approved — revert to ACC-{yyyy}-{seq} style**
- [ ] Deferred

---

### #C5 — Master titles (Income vs Revenue, Equity)

**Recommended:** Keep current five masters (Assets, Equity, Liabilities, Income, Expense) unless finance naming must match BRD examples exactly.

- [ ] **Approved — keep current seed names**
- [ ] **Approved — rename Income→Revenue (and any other renames listed):** ________
- [ ] Deferred

---

## F. DO NOW implementation notes (after clarifications)

### #D3 + #D1 — Product → Inventory subsidiary (outline)

1. Extend `ISubsidiaryAccountNoAllocator` with `AllocateProductAsync` → AccountNo + `{Tenant}-P{seq5}`.
2. In Product create TX: resolve Control where `ControlKind = INVENTORY`; insert Subsidiary (`IsSystemGenerated = true`, title = Product Name); set `Product.SubsidiaryAccountId`.
3. Fail closed if INV control missing.
4. Block delete/deactivate when `IsSystemGenerated`.

### #D2 — Vendor → AP subsidiary (outline)

1. Same pattern with `AllocateVendorAsync` → `{Tenant}-V{seq5}` under `ACCOUNTS_PAYABLE`.
2. Depends on Vendor module existence.

### #C8 — Master Prefix DB unique

1. Add unique index on `MasterAccounts.Prefix` (after data clean).
2. Keep API validation.

### #D7 — Cash/Bank protection (optional later)

1. Mark baseline Cash/Bank `IsSystemGenerated = true` **or** dedicated baseline flag.
2. Block soft-delete like automation rows.

---

## G. Claims check vs BRD §3.2

| Claim family | Super Admin | Tenant Admin (seed) | BRD §3.2 | Gap |
|--------------|-------------|---------------------|----------|-----|
| `coa.masters.*` | Full | Read only | Aligns | — |
| `coa.controls.*` | Full | Full Own Tenant | Aligns (#C1/#D4 Done) | — |
| `coa.subsidiaries.*` | Full + View-all list | Full CRUD | Aligns | — |
| `FINANCE` role | Template | Control/Subsidiary full + Masters read | Configurable ops (#D5 Done) | — |

---

## H. Acceptance checklist (Accounts M1 vs BRD)

- [x] #C1 decision recorded (platform exception **or** tenant Controls plan)
- [x] #C2 / #C3 coding formats stakeholder-approved
- [x] Hierarchy Master → Control → Subsidiary enforced (FK RESTRICT) — **already Done**
- [x] INV + AP control nodes exist for automation — per-company seed + Product/Vendor hooks
- [x] Product create opens Inventory subsidiary + system lock
- [x] Vendor create opens AP subsidiary + system lock
- [x] Manual subsidiary delete allowed; Product/Vendor-linked delete blocked (`IsSystemGenerated`)
- [x] JWT tenancy on Subsidiary writes — **Done**
- [x] SA Subsidiary View-all — **Done** (T1.64)

---

## I. Task Matrix linkage

| Plan item | Suggested Task ID (when started) |
|-----------|----------------------------------|
| This document | **T1.77** |
| #C1 decision / exception flag | T1.78 |
| #C8 Prefix unique index | T1.79 |
| #D3 allocator P/V | T1.80 |
| #D1 Product COA TX | T1.81 (or M3 wave) |
| #D2 Vendor COA TX | T1.82 (or M4 wave) |

---

*End of Accounts Modules BRD Gap Resolve Plan. No “beyond BRD extras” inventory — only mismatches and missing BRD/Task items.*
