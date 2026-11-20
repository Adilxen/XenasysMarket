# Xenasys Market Place — Database Design Plan

**Document type:** Architectural blueprint  
**Aligned to:** `Xenasys_Implementation_Sequence.md` (M0–M7) · BRD v1.1 · **T0.4 INT IDENTITY + CHAR(16) product_code**  
**Target RDBMS:** Microsoft SQL Server (EF Core)  
**Scale goal:** Daraz-class catalogs — **sub-second (<1s)** tenant-scoped lookups via composite indexes + keyset pagination (no `OFFSET` on hot paths)

> **Convention:** Column `company_id` is the tenant boundary key (BRD also says `tenant_id`). JWT claim `tenant_id` maps to `company_id` at the application layer. Do **not** accept `company_id` from client payloads for isolation.

---

## 0. Global design decisions

### 0.1 Primary key strategy (T0.4 — MANDATORY)

| ID type | Use | Rationale |
|---------|-----|-----------|
| **`INT IDENTITY(1,1)`** | Default surrogate PK (`id`) on all core tables | Compact clustered index, sequential inserts, minimal page splits vs random GUID |
| **`BIGINT IDENTITY(1,1)`** | High-volume ledgers only if INT range insufficient (e.g. future `stock_movements`) | Reserve for append-only mega-tables |
| **`CHAR(16)` business key** | `products.product_code` | Fixed-width composite join key for inventory sub-tables (see §0.5) |

**Rules:**

1. Surrogate PK = **`INT IDENTITY(1,1)`** — never UUID for row identity.  
2. Cross-table FKs reference `id` (INT).  
3. Inventory / transactional child tables that resolve a product in hot JOINs **also** store and index **`product_code CHAR(16)`** for string-join paths (denormalized business key under tenant).  
4. Alternate uniqueness remains tenant-scoped: e.g. `UNIQUE (company_id, product_code)`.

### 0.2 Delete / cascade policy (platform-wide)

| Relationship | `ON DELETE` | Why |
|--------------|-------------|-----|
| Parent catalog → children with active products | **RESTRICT** | Prefer soft-deactivate |
| Category → Sub-Category | **RESTRICT** | Prevent orphan sub-categories |
| Control → Subsidiary | **RESTRICT** | No floating subsidiaries |
| Product/Vendor → Subsidiary | **RESTRICT** | Preserve GL trail |
| Tenant → tenant-scoped rows | **RESTRICT** | Soft-deactivate tenant |
| Junction tables (role_permissions) | **CASCADE** | Pure link rows |

### 0.3 Soft-delete vs hard-delete

Prefer **`is_active BIT`** over physical deletes for catalog, products, vendors, accounts, users.

### 0.4 Naming

- Tables: `snake_case` plural  
- Columns: `snake_case`  
- Indexes: `ix_<table>_<cols>` · Unique: `ux_<table>_<cols>`

### 0.5 Fixed 16-character `product_code` strategy (T0.4)

#### 0.5.1 Column definition

```sql
product_code CHAR(16) NOT NULL
```

#### 0.5.2 Concatenation formula (exactly 16 characters)

```text
[CompanyNo 2] + [ShortName 3] + [UserNn 2] + [Sequence 9]  = 16
Example:     0 1   X E N         0 1         0 0 0 0 0 0 0 0 1
```

| Segment | Length | Source | Rules |
|---------|--------|--------|--------|
| Company ID | 2 | `Tenants.CompanyNo` | Digits `00`–`99`, zero-padded |
| Short Company Name | 3 | `tenants.short_name` | Uppercase `A–Z0–9`, exactly 3 chars |
| User Creator ID | 2 | `users.user_nn` | Digits `00`–`99`, creator at insert |
| Sequential Int | 9 | `product_code_sequences.last_value` | Zero-padded per company |

```csharp
ProductCodeGenerator.Generate(companyNn, shortName, userNn, sequence);
// CHAR(16); throws if segment lengths invalid
```

#### 0.5.3 Uniqueness + covering index

```sql
CREATE UNIQUE NONCLUSTERED INDEX ux_products_company_product_code_covering
  ON products(company_id, product_code)
  INCLUDE (product_name, retail_price, minimum_stock_level, is_active);
```

Ironclad boundary: **`UNIQUE (company_id, product_code)`**.

#### 0.5.4 Inventory sub-table join rule

Hot JOINs must use `product_code` (with `company_id`):

```sql
FROM stock_movements sm
INNER JOIN products p
  ON p.company_id = sm.company_id
 AND p.product_code = sm.product_code
```

Child tables store `product_code CHAR(16)` (and may also keep `product_id` INT FK for RI).

#### 0.5.5 Sequence table

```text
product_code_sequences
  company_id   INT  PK FK → tenants(id)
  last_value   BIGINT NOT NULL
```

---

## 1. Audit & metadata

| Column | Type | Notes |
|--------|------|--------|
| `insert_date` | `datetime2(3)` | NOT NULL |
| `insert_user_id` | **`int`** | JWT user → `users.id` |
| `update_date` | `datetime2(3)` | NULL |
| `update_user_id` | **`int`** | NULL |

```text
ITenantScoped.company_id  int NOT NULL → tenants(id) RESTRICT
```

---

## 2. Core tables (T0.4)

### `tenants`

| Column | Type |
|--------|------|
| `id` | `int IDENTITY(1,1)` PK |
| `name` | `nvarchar(200)` |
| `TenantCode` | **`char(6)`** UNIQUE (`UX_Tenants_TenantCode`) |
| `CompanyNo` | **`char(2)`** |
| `ShortName` | **`char(3)`** |
| `IsActive` | `bit` |
| `IsFrozen` | **`bit` default 0** — marketplace kill-switch |
| + audit | `InsertUserId` int via `BaseAuditableEntity` |

### `users`

| Column | Type |
|--------|------|
| `id` | `int IDENTITY(1,1)` PK |
| `user_name` | `nvarchar(50)` UNIQUE |
| `user_nn` | **`char(2)`** |
| `password_hash` | `nvarchar(200)` |
| `is_super_admin` | `bit` |
| + audit | |

### `products`

| Column | Type |
|--------|------|
| `id` | `int IDENTITY(1,1)` PK clustered |
| `company_id` | `int` FK |
| `product_code` | **`char(16)`** NOT NULL |
| `category_id` / `sub_category_id` | `int` NULL (until M2) |
| `product_name` | `nvarchar(150)` |
| `packaging` | `nvarchar(50)` |
| `unit_of_measurement` | `nvarchar(20)` |
| `minimum_stock_level` | `int` |
| `invoice_price` / `retail_price` | `decimal(10,2)` |
| `image_path` | `nvarchar(500)` |
| `is_active` | `bit` |
| `subsidiary_account_id` | `int` NULL |
| + audit | |

**Indexes:**

```sql
CREATE UNIQUE NONCLUSTERED INDEX ux_products_company_product_code_covering
  ON products(company_id, product_code)
  INCLUDE (product_name, retail_price, minimum_stock_level, is_active);

CREATE NONCLUSTERED INDEX ix_products_company_id
  ON products(company_id, id);  -- keyset
```

### COA

`master_accounts` / `control_accounts` / `subsidiary_accounts` — PK `int IDENTITY`.  
`subsidiary_accounts.product_code CHAR(16) NULL` when product-sourced.

### `stock_movements` (M5)

Must include `product_code CHAR(16)` + index `(company_id, product_code, occurred_at DESC)`.

---

## 3. Chart of Accounts

Master → Control → Subsidiary unchanged. Control kinds `INVENTORY` / `ACCOUNTS_PAYABLE` unique filtered. PKs are **INT**.

---

## 4. Migration note (T0.4)

Phase 0 local DBs: **drop and recreate** `XenasysMarketplace` with fresh `InitialCreate_T0_4`. Do not convert UUID→INT in place.

---

*T0.4 INT IDENTITY + CHAR(16) product_code rules supersede any prior UUID PK guidance in older drafts.*
