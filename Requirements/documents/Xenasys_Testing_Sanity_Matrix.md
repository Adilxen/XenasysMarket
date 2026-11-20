# Xenasys Testing Sanity Matrix — Multi-Tenant Isolation & Regression

**Purpose:** Automated scenario baseline so Daraz-scale features cannot ship with cross-tenant leakage or broken COA/SKU automations.  
**Aligned to:** `Xenasys_Task_Matrix.md`, `Xenasys_API_Specification.md`, `Xenasys_Database_Design.md`  
**Stacks:** xUnit + WebApplicationFactory (.NET) · Playwright or Vitest/RTL (frontend) · optional k6 smoke later

---

## 1. Isolation doctrine

| Rule ID | Doctrine |
|---------|----------|
| **ISO-1** | Tenant A token must never read/write Tenant B rows |
| **ISO-2** | Client-supplied `companyId` in body/query must not widen scope |
| **ISO-3** | Missing/invalid JWT → **401**; wrong role → **403**; other-tenant resource → **404** (prefer) |
| **ISO-4** | EF global filters remain on for tenant roles; Super Admin cross-tenant requires explicit tests |
| **ISO-5** | Cache miss/fill must use JWT company id in Redis keys (no key confusion) |

Every new tenant-scoped endpoint must add at least one **ISO-1** automated case before merge.

---

## 2. Fixture model (backend)

Seed once per test run (or per collection):

| Fixture | Description |
|---------|-------------|
| `TenantA` | Company A + Admin user A + JWT A |
| `TenantB` | Company B + Admin user B + JWT B |
| `SuperAdmin` | Platform JWT (`SUPER_ADMIN`) |
| `SharedMasters` | Units + Master/Control COA seed (`INVENTORY`, `ACCOUNTS_PAYABLE`) |
| `CatalogA` | Category/Sub/Brand under A (when M2+) |
| `ProductA` | Product under A with subsidiary (when M3+) |
| `VendorA` | Vendor under A with AP subsidiary (when M4+) |

**Helpers:**

```text
CreateClientWithJwt(tenantId, userId, role)
AssertStatus(response, expected)
AssertEnvelopeSuccess / AssertEnvelope422 / AssertEnvelope401
```

---

## 3. Automated scenario matrix

### 3.1 M0 — Identity & tenancy

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M0-01** | Login requires company | POST login without `companyId` | **422** fields.companyId | 0.4 |
| **T-M0-02** | Login success binds tenant | Valid user+company | **200** + JWT with `tenant_id` | 0.1 |
| **T-M0-03** | Invalid password | Wrong password | **401** INVALID_CREDENTIALS | 0.3 |
| **T-M0-04** | Reject body company override | Authenticated as A; GET resource with query `companyId=B` | Still only A data / ignore B | **0.2** |
| **T-M0-05** | Expired token | Call API with expired JWT | **401** | 0.1 |
| **T-M0-06** | Membership isolation | User only in A; login with B company id | **422** or **401** | 0.4, 0.7 |

### 3.2 Cross-tenant read/write (core ISO)

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-ISO-01** | A cannot GET B product by id | JWT A → `GET /api/products/{productB}` | **404** | ISO-1, 0.2 |
| **T-ISO-02** | A cannot PUT B product | JWT A → update productB | **404** or **403** | ISO-1 |
| **T-ISO-03** | A list never contains B ids | JWT A → keyset list | No B ids in `items` | ISO-1, 3.8 |
| **T-ISO-04** | A cannot GET B vendor | Same pattern | **404** | ISO-1 |
| **T-ISO-05** | A cannot GET B category | Same pattern | **404** | ISO-1 |
| **T-ISO-06** | A cannot delete B subsidiary | Attempt delete | **404/403/409** | 1.6 |
| **T-ISO-07** | Super Admin list with tenant filter | JWT Super + tenant selector A | Only A (or aggregate explicitly tested) | 7.3 |
| **T-ISO-08** | Tenant Admin blocked from Super routes | JWT A → `/api/super/*` | **403** | 6.2, 7.4 |

### 3.3 M1 — COA integrity

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M1-01** | Tenant cannot POST master account | JWT A → create master | **403** | **1.1** |
| **T-M1-02** | Tenant can GET masters | JWT A → list masters | **200** read-only data | 1.1 |
| **T-M1-03** | Subsidiary requires control FK | Insert without control | DB/API failure | 1.4 |
| **T-M1-04** | Duplicate `account_no` same tenant | Second insert same no | Unique violation → **422/409** | **1.2** |
| **T-M1-05** | Delete system subsidiary blocked | Delete `is_system_generated=1` | **409** | 1.6 |
| **T-M1-06** | Inventory + AP controls seeded | Query by `control_kind` | Exactly one each | 1.5 |

### 3.4 M2 — Catalog

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M2-01** | Duplicate category name same tenant | Create two "Electronics" in A | **422** name | 2.1 |
| **T-M2-02** | Same category name different tenants | "Electronics" in A and B | Both **201** | 2.1, ISO-1 |
| **T-M2-03** | Sub-category wrong parent company | Point B sub to A category | **422/404** | 2.3 |
| **T-M2-04** | Delete category with products | Attempt hard delete | **409** / RESTRICT | 2.4 |
| **T-M2-05** | Image > 2MB / bad extension | Upload | **422** | 2.2 |

### 3.5 M3 — Product + SKU + subsidiary TX

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M3-01** | Create product atomic COA | POST product | Product + inventory subsidiary + link in one TX | **3.1**, 3.6 |
| **T-M3-02** | Rollback on subsidiary failure | Force control missing | No orphan product row | **3.1** |
| **T-M3-03** | Retail < Invoice rejected | POST prices 10 / 5 | **422** retailPrice | **3.2** |
| **T-M3-04** | Missing category rejected | Omit categoryId | **422** | 3.3 |
| **T-M3-05** | Sub-category not under category | Mismatched pair | **422** | 3.4 |
| **T-M3-06** | Duplicate SKU same tenant | Collision | **422/409** | 3.5 |
| **T-M3-07** | Keyset does not use OFFSET | Inspect SQL or EF log on list | No `OFFSET` | X.1, 3.8 |

### 3.6 M4 — Vendor + NTN + AP subsidiary

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M4-01** | Vendor create opens AP subsidiary | POST vendor | Subsidiary under AP control | **4.1** |
| **T-M4-02** | NTN mask enforcement | `123-45` | **422** ntnNo | **4.2** |
| **T-M4-03** | Valid NTN `1234567-8` | Proper format | Accept (if unique) | 4.2 |
| **T-M4-04** | Duplicate NTN same tenant | Second insert | **422/409** | 4.2 |
| **T-M4-05** | Phone regex fail | `123` | **422** | 4.3 |
| **T-M4-06** | A cannot read B vendor subsidiary via id | Cross GET | **404** | ISO-1 |

### 3.7 M5 — Stock alerts (non-blocking)

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-M5-01** | Qty ≤ min stock enqueues outbox | Patch stock | Outbox row PENDING; HTTP returns without waiting notification send | 5.2, X.2 |
| **T-M5-02** | Stock on other tenant product | JWT A patches B | **404** | ISO-1 |

### 3.8 Cache isolation (when Redis enabled)

| Test ID | Scenario | Steps | Expected | Task refs |
|---------|----------|-------|----------|-----------|
| **T-CACHE-01** | Warm A category list; switch to B | Ensure B response ≠ A payload | Keys include company id | Redis arch §4 |
| **T-CACHE-02** | Mutate A category | Next A list miss/fresh | Invalidation ran | Redis §5 |
| **T-CACHE-03** | B unaffected by A invalidation | B still coherent | No global flush required | ISO-5 |

### 3.9 Frontend sanity (smoke)

| Test ID | Scenario | Expected |
|---------|----------|----------|
| **T-UI-01** | Login blur empty username | Red `#DC2626` message on blur |
| **T-UI-02** | Fix field clears error on input | Error disappears when schema satisfied |
| **T-UI-03** | 422 from API maps under field | Server message under matching input |
| **T-UI-04** | 401 redirects to `/login` | Session cleared |

---

## 4. CI gate recommendations

| Stage | Must pass |
|-------|-----------|
| PR | `T-M0-04`, `T-ISO-01`, `T-ISO-03` (once products exist); always compile + unit |
| Pre-merge M3 | `T-M3-01` … `T-M3-07` |
| Pre-merge M4 | `T-M4-01` … `T-M4-06` |
| Nightly | Full ISO + CACHE matrix |

**Fail the build** if any ISO-* test fails — treat as Sev-0.

---

## 5. Test naming convention

```text
{Area}_{Behavior}_{Expected}
Example: Isolation_GetProduct_OtherTenant_Returns404
```

Project folders (planned):

```text
backend/tests/Xenasys.Tests.Unit/
backend/tests/Xenasys.Tests.Integration/
frontend/tests/e2e/   (optional Playwright)
```

---

## 6. Phase 0 minimum set (implement first)

Before heavy M1+ features, automate:

1. **T-M0-01** Login 422 without company  
2. **T-M0-02** JWT contains `tenant_id`  
3. **T-M0-04** Client `companyId` ignored for isolation  
4. **T-M0-05** Invalid JWT → 401  
5. Envelope assertions for **200 / 401 / 422** per `Xenasys_API_Specification.md`

---

## 7. Audit log reminder

When adding tests or endpoints, append `Xenasys_Task_Matrix.md` §3 with Pre-Check **Passed/Failed** before coding. Isolation tests map primarily to Tasks **0.1, 0.2, 0.8**.

---

*End of Testing Sanity Matrix.*
