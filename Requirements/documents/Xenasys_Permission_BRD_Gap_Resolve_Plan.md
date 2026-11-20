# Permission Module × BRD Gap — Resolve Plan

**Source:** `Xenasys_Market_Place_BRD_v1.1.docx` (§2, §3, §4.1)  
**Code baseline:** Form×Action RBAC (Users / Roles / Forms / Actions / Company)  
**Created:** 2026-09-15  
**Task Matrix:** T1.55 (plan) → implementation IDs T1.56+ as each item starts  

---

## How to use this file

1. Items marked **DO NOW** → implement one-by-one in order below.
2. Items marked **SKIP (stakeholder)** → no code this wave.
3. Items marked **CLARIFY** → recommended answer + decision checkbox; implement only after you tick **Approved**.
4. After each DO NOW item ships: mark Status = Done and append Task Matrix audit row.

### Resolve order (recommended)


| Wave      | Item                                                  | Why this order                                     |
| --------- | ----------------------------------------------------- | -------------------------------------------------- |
| **1**     | **#1** Multi-company assign (Users module)            | Unlocks BRD login multi-company; foundation for #C |
| **2**     | **#C** Super Admin edit/delete membership scope       | Depends on multi-membership model                  |
| **3**     | **#D** `company_id` wire policy (document + harden)   | Same create/edit APIs as #1/#C                     |
| **4**     | **#2** Tenant Admin company profile (header)          | Independent; small surface                         |
| **5**     | **#4** Audit accuracy decision / optional central log | Independent                                        |
| **Later** | Ops matrix (#6), A, B                                 | After clarifications approved                      |


**Skipped this wave:** #3 bcrypt · #5 Super Admin dashboard · #E Forms/Actions screens · #F TENANT_STAFF name  

---

## Status legend


| Status      | Meaning                            |
| ----------- | ---------------------------------- |
| Pending     | Not started                        |
| Ready       | Clarified / plan locked — can code |
| In progress | Coding                             |
| Done        | Accepted                           |
| Skipped     | Explicitly out of scope this wave  |
| Blocked     | Waiting stakeholder tick           |


---

# DO NOW

## #1 — Same user → multiple companies (Users module)


|                     |                                                                                                            |
| ------------------- | ---------------------------------------------------------------------------------------------------------- |
| **BRD**             | Login: username ki jitni active companies, unmein se choose. Ek user kai companies se linked ho sakta hai. |
| **Ab**              | Create pe ek company; baad mein doosri company assign ka dedicated flow nahi (T1.37 rolled back).          |
| **Stakeholder fix** | **Super Admin Users module se hi** multiple companies assign kare.                                         |
| **Status**          | **Done** (T1.56)                                                                                           |
| **Task ID**         | T1.56                                                                                                      |


### Simple meaning

Ali pehle ABC pe staff hai. Super Admin Users drawer se Ali ko XYZ bhi assign kare → Ali login pe ABC / XYZ choose kar sake.

### Implementation plan

**Data (already exists):** `UserTenantMembership` (UserId + CompanyId) + `UserRoles` per company. No new table required.

**API (UsersController — Super Admin only for multi-assign):**


| Endpoint                                                   | Behavior                                                                                   |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `GET /api/users/{id}`                                      | Already returns `companies[]` — keep / enrich with roles per membership                    |
| `POST /api/users/{id}/memberships` **(new)**               | Body: `{ company_id, roleIds[] }` → add membership if missing; sync roles for that company |
| `PUT /api/users/{id}/memberships/{companyId}` **(new)**    | Update roles / `IsActive` for that membership only                                         |
| `DELETE /api/users/{id}/memberships/{companyId}` **(new)** | Soft-deactivate that membership + that company’s UserRoles                                 |


**Rules:**

- Super Admin only on membership add/remove across companies.  
- Tenant Admin: create/update **only JWT company** (unchanged).  
- Same username + same company active membership → 422.  
- Cannot assign `SUPER_ADMIN` via this flow.  
- Platform Super Admin user row: never add tenant memberships via this UI.  
- At least one active membership required before deactivating “last” company (or block with clear error).

**Frontend (`users-listing-page` drawer — Super Admin):**

- Section **Companies** (not single dropdown only):  
  - List existing memberships (name, status, roles).  
  - **Add company** → SearchableCompanySelect + multi-role (roles filtered by selected company + platform `TENANT_ADMIN`).  
  - Edit roles / deactivate per row.
- Create flow: first membership = primary (same as today); optional “Add another company” before Save **or** Save then reopen Edit to add (prefer Edit/Add after create for simpler first ship).

**Accept / verify:**

1. Super Admin: user `ali` → add Company A + Company B.
2. Login as `ali` without company → challenge lists A + B.
3. Choose B → JWT `tenant_id=B`; data only B.
4. Tenant Admin cannot call add-membership for another company → 403.

---

## #2 — Tenant Admin own company profile (header)


|                     |                                                                               |
| ------------------- | ----------------------------------------------------------------------------- |
| **BRD**             | Company Setup → Tenant Admin = Read / Edit **Own** Company.                   |
| **Ab**              | `/settings/company` seed = Super Admin only (TA nav bahar).                   |
| **Stakeholder fix** | Tenant Admin **header** se — User Profile ke sath **Company Profile** update. |
| **Status**          | **Done** (T1.57)                                                              |
| **Task ID**         | T1.57                                                                         |


### Simple meaning

Tenant Admin sidebar Company listing nahi kholega. Header → Profile drawer mein do blocks: **My Profile** + **My Company**.

### Implementation plan

**Do not** re-open `/settings/company` for Tenant Admin (platform catalog stays Super Admin).

**API options (pick A — simpler):**


| Option              | Approach                                                                                                                                                                                   |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **A (recommended)** | Reuse `PUT /api/tenants/{id}` with `company.update` + `CanAccessTenant` (already own-only for TA). Grant Tenant Admin `company.update` **without** granting `/settings/company` form path. |
| B                   | New `GET/PUT /api/auth/me/company` wrapper that always uses JWT `tenant_id`.                                                                                                               |


**Claim / seed:**

- Ensure `TENANT_ADMIN` has action **Update** on a form that maps to `company.update` **or** bridge `company.update` without exposing Company listing form.  
- Prefer: keep Form path `/settings/company` Super Admin only; add claim grant via RoleFormAction on a **hidden/header-only** path **or** allow `company.update` derived from existing bridge for JWT company update only.  
- Practical ship: grant `company.update` to platform `TENANT_ADMIN` Form×Action on `/settings/company` **Read+Update only**, but **do not** show Company in sidebar (nav filter stays PlatformAdminOnly). Header uses API directly.

**Frontend (`app-header` Profile drawer):**

1. Keep: User Name, Password (optional).
2. Add section **Company profile** (Tenant Admin + when JWT has company): Name, Contact Email, Phone, Address (read Company Code disabled).
3. Super Admin with null company: hide Company section (or show “Platform — no company”).
4. Save: `updateMyProfile` + `updateTenant(jwtCompanyId, …)` in one primary action (or two Save buttons — prefer one Save).

**Accept / verify:**

1. Tenant Admin opens Profile → changes company phone → DB tenant row updates.
2. Cannot `PUT /api/tenants/{otherId}` → 403.
3. Company still absent from Tenant Admin sidebar.

---

## #4 — Super Admin central audit log


|                 |                                                                                   |
| --------------- | --------------------------------------------------------------------------------- |
| **BRD**         | Super Admin operations **central audit table** mein log.                          |
| **Ab**          | Row-level `InsertUserId` / `UpdateUserId` / dates (`AuditClock` + `SaveChanges`). |
| **Stakeholder** | Agar yeh way **100% accurate** hai to OK; warna central audit implement.          |
| **Status**      | **Done** (T1.58 — 4b PlatformAuditLogs)                                           |
| **Task ID**     | T1.58 (only if implement)                                                         |


### Is row-level audit 100% enough?


| What row-level **does** well            | What it **does not** cover                         |
| --------------------------------------- | -------------------------------------------------- |
| Who created / last changed **this row** | Failed / forbidden attempts                        |
| When (PKT stamps)                       | Field-level before/after diff                      |
| Works on every auditable entity         | One place to “show all Super Admin actions today”  |
| Soft-delete usually stamps Update*      | Cross-table narrative without querying many tables |


**Verdict:** Row-level is **accurate for data stewardship** (BRD §2 Audit Metadata) and already Done (S1).  
It is **not** a full substitute for BRD §3.1 wording *“central audit table”* for Super Admin **operations**.

**Recommendation:**

- **Phase 4a (this wave — lightweight):** Keep row-level as SSoT for entity history. Document: “§2 metadata = Done; §3.1 central ops log = deferred unless needed for compliance UI.”  
- **Phase 4b (implement if you need SA activity screen):** Add `PlatformAuditLogs` table:

```text
Id, ActorUserId, ActorUserName, Action (Create/Update/Delete/Login),
EntityType, EntityId, CompanyId?, Summary, PayloadJson?, InsertDate
```

Write only when `IsSuperAdmin` mutates: Tenants, Users/memberships, Roles, Forms, Actions, platform seeds.  
UI later: `/settings/permission-audit` sibling or System → Audit.

**Stakeholder tick:**

- [ ] **4a only** — row-level enough for now (no new table)  
- [x] **4b** — implement central `PlatformAuditLogs` now  

Default if no tick: **4a** (no code until you ask).

---

# SKIP THIS WAVE (locked)


| #      | Topic                              | Decision                                        |
| ------ | ---------------------------------- | ----------------------------------------------- |
| **#3** | Password bcrypt                    | **No change** — plaintext TEMP OK for now       |
| **#5** | Multi-tenant Super Admin dashboard | **No change**                                   |
| **#E** | Forms / Actions screens not in BRD | **OK** — Form×Action = implementation of matrix |
| **#F** | `TENANT_STAFF` retired             | **OK** — custom roles replace named staff       |


---

# CLARIFY (recommended answers — tick to unlock)

## #6 — Operational modules full matrix enforcement


|             |                                                                                                     |
| ----------- | --------------------------------------------------------------------------------------------------- |
| **Gap**     | Roles pe Form×Action hai; Category/Brand/Product/Stock/Vendor/COA pe BRD matrix wiring “Done” nahi. |
| **Status**  | **Approved** — module-by-module (defer code to ops modules)                                         |
| **Task ID** | T1.59+ (per module)                                                                                 |


### Clarification (simple)

**Permission screens = “remote control.”**  
**Ops modules = “TV.”** Remote pe buttons hain; TV pe signal tab tak nahi jab tak har module API pe `RequirePermission` + FE ClaimGate lagay.

**Example:** Role pe Products Create tick → Products `POST` pe `products.create` check hona chahiye. Tick hone se auto-enforce nahi hota.

### Recommended decision

- **Now:** Permission catalog (Forms/Actions/Roles) complete rakho.  
- **Next (Module 6 / per ops module):** jab Category/Product/… ship ho, usi PR mein claims + seed grants + 403 tests.  
- **Do not** big-bang “enforce everything” before modules exist.

**Tick:**

- [x] Approved: enforce **module-by-module** with each ops delivery  
- [ ] Other: _______________________

---

## #A — Super Admin “System Users Only” vs tenant user provision


|                |                                                                                               |
| -------------- | --------------------------------------------------------------------------------------------- |
| **BRD matrix** | User & Role Management → Super Admin = **System Users Only**; Tenant Admin = Own tenant full. |
| **Ab**         | Super Admin kisi bhi company ke users/roles create karta hai (Tenant Admin provision).        |
| **Status**     | **Approved** — SA may provision tenant users                                                          |


### Clarification (simple)

BRD matrix line **strict** padho to Super Admin tenant staff nahi banata.  
Practical marketplace: pehli company ke baad **kisi ko** Tenant Admin banana padta hai — woh Super Admin hi karega.

**Recommended reading (accepted exception):**

- “System Users Only” = Super Admin **platform** account + **bootstrap** Tenant Admins / cross-company support.  
- Day-to-day staff users = Tenant Admin.  
- Keep current behavior; document as **approved BRD exception**.

**Tick:**

- [x] Approved: SA may provision tenant users/roles (current behavior)  
- [ ] Restrict: SA only platform users; tenant users = TA only (breaks first-company bootstrap unless separate invite flow)

---

## #B — Tenant Admin “full company domain” vs platform-only Settings


|            |                                                                                                                             |
| ---------- | --------------------------------------------------------------------------------------------------------------------------- |
| **BRD**    | TA = company settings + users + roles + ops.                                                                                |
| **Ab**     | Actions / Forms / Permission Audit / Company listing = Super Admin. TA = Roles + Users (+ header company profile after #2). |
| **Status** | **Approved** — platform Settings SA-only; TA via header (#2)                                                                |


### Clarification (simple)

- **Forms / Actions / Permission Audit** = platform wiring (global catalog). Tenant Admin ko yeh badalne ki zaroorat nahi — warna har company alag “Create” action bana sakti hai.  
- **Company settings (profile)** = BRD TA right → cover by **#2 header**, not full Company registration screen.  
- **Users + Roles + ops modules** = TA domain (correct).

**Recommended decision:** Keep platform-only Settings for Forms/Actions/Audit/Company **listing**. Ship **#2** for own company profile. No change to nav seed beyond #2 claims.

**Tick:**

- [x] Approved: platform Settings stay SA-only; TA company profile via header (#2)  
- [ ] Other: re-open Company listing for TA (not recommended)

---

## #C — Super Admin edit/delete stuck on JWT company


|             |                                                                                                                          |
| ----------- | ------------------------------------------------------------------------------------------------------------------------ |
| **Gap**     | SA create: company dropdown OK. SA edit/delete: JWT company membership only → list mein doosri company dikhe, edit fail. |
| **Status**  | **Done** (with #1 / T1.56)                                                                                               |
| **Task ID** | T1.56 companion / T1.56b                                                                                                 |


### Clarification (simple)

List “all companies” dikhana + edit “sirf login company” = broken UX.

**Recommended fix (with #1):**

- Super Admin Update/Delete target = **selected membership’s `company_id`** (route or body), not ambient JWT alone.  
- Tenant Admin: still JWT-only (ignore client company).  
- UI: when SA opens user, pick membership row (company) then edit roles/status for **that** company; or edit user profile fields once (global User row) + per-company membership panel.

**Tick:**

- [x] Approved: SA edit/delete by membership company (implement with #1)  
- [ ] Other: _______________________

---

## #D — `company_id` from client on create (BRD §2)


|             |                                                                                                       |
| ----------- | ----------------------------------------------------------------------------------------------------- |
| **BRD §2**  | Isolation key JWT se; client `company_id` se query trust mat karo.                                    |
| **Ab**      | Super Admin user/role **create** pe body `company_id` accept. TenantClaimGuard **skips** Super Admin. |
| **Status**  | **Done** (T1.60)                                                                                      |
| **Task ID** | T1.60 (policy harden + docs)                                                                          |


### Solution (findings)

BRD §2 is about **isolation of data reads/writes**, not about “Super Admin may never name a target company when provisioning.”


| Pattern                                                           | Safe?                  | Notes                                                                         |
| ----------------------------------------------------------------- | ---------------------- | ----------------------------------------------------------------------------- |
| Tenant user sends `company_id` ≠ JWT                              | **No**                 | Already blocked by `TenantClaimGuard`                                         |
| Super Admin create user with `company_id=5`                       | **Yes (provisioning)** | Creates membership in 5; does not change JWT isolation for later tenant calls |
| Any user uses body `company_id` to **read** other tenant products | **No**                 | EF filters + JWT `ITenantProvider`                                            |
| Super Admin edit using only JWT when listing all companies        | **Broken UX**          | Fix via #C                                                                    |


### Complete plan for #D

1. **Document accepted exception** in this file + Task Matrix:
  *“Inbound `company_id` on Super Admin **create** (users/roles/memberships) = provisioning target. Isolation SSoT remains JWT `tenant_id` for all tenant-scoped queries. Tenant Admin never trusts body `company_id`.”*
2. **Code harden (small):**
  - Non–Super Admin: strip/ignore request `company_id` (already `TryResolveCreateCompanyId`).  
  - Super Admin create: require `company_id > 0` + active tenant FK check (already).  
  - Never use body `company_id` inside generic list/filter for tenant roles (already JWT / SA bypass patterns).  
  - Optional: rename DTO comment to `target_company_id` in docs only — **keep wire key `company_id`** (project SSoT).
3. **Alternative rejected for now:** “SA must switch JWT company then create” — worse UX, more session bugs.
4. **Accept:** Tenant Admin spoof `company_id` → guard 403; SA create with valid id → 201; SA create without id → 422.

**Tick:**

- [x] Approved: keep SA create `company_id` + document exception + harden (#D plan)  
- [ ] Other: force SA tenant-switch before create

---

# Implementation checklist (DO NOW only)

When you say **“#1 start”**, execute in this order:

### T1.56 — Multi-company (#1) + SA membership edit (#C if approved)

- [ ] API: add/update/deactivate membership endpoints (SA)  
- [ ] API: Update user profile fields without requiring JWT==target company (SA); membership ops keyed by `company_id`  
- [ ] FE Users drawer: companies list + add company + roles per company  
- [ ] Verify multi-company login challenge  
- [ ] Task Matrix row Passed  

### T1.57 — Header company profile (#2)

- [ ] Seed/claim: TA can `company.update` own tenant without Company nav  
- [ ] Header Profile: company fields + save via tenants API  
- [ ] Verify cross-company forbid  
- [ ] Task Matrix row Passed  

### T1.58 — Audit (#4) — only if 4b ticked

- [ ] Entity + migration `PlatformAuditLogs`  
- [ ] Write on SA mutations  
- [ ] Optional read API / stub UI  

### T1.60 — #D policy

- [ ] Comments + this doc exception flagged in Task Matrix  
- [ ] Guard/resolve regression tests  

---

## Decision log (fill as you go)


| Date       | Item                   | Decision                              | Actor       |
| ---------- | ---------------------- | ------------------------------------- | ----------- |
| 2026-09-15 | #3 bcrypt              | Skip                                  | Stakeholder |
| 2026-09-15 | #5 SA dashboard        | Skip                                  | Stakeholder |
| 2026-09-15 | #E Forms/Actions       | Keep                                  | Stakeholder |
| 2026-09-15 | #F TENANT_STAFF        | Keep                                  | Stakeholder |
| 2026-09-15 | #1 multi-company       | Fix via Users module (SA) — **Done T1.56** | Stakeholder |
| 2026-09-15 | #2 company profile     | Header My Company (TA) — **Done T1.57**   | Stakeholder / Agent |
| 2026-09-15 | #4 audit               | **4b Done T1.58** PlatformAuditLogs   | Stakeholder / Agent |
| 2026-09-15 | #D company_id policy   | **Done T1.60** SA create exception documented | Stakeholder |
| 2026-09-15 | #C SA membership scope | **Done** with T1.56                           | Stakeholder |
| 2026-09-15 | #6 ops matrix          | Approved module-by-module (later)             | Stakeholder |
| 2026-09-15 | #A SA provision users  | Approved current behavior                      | Stakeholder |
| 2026-09-15 | #B platform Settings   | Approved SA-only + header company             | Stakeholder |


---

*End of Resolve Plan. **Permission BRD gap wave complete.** #6 enforcement ships with each ops module.*