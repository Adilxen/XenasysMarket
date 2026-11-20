# Xenasys Redis Cache Architecture — Low-Latency Lookup Blueprint

**Purpose:** Sub-second catalog and master-data reads under Daraz-scale traffic without weakening multi-tenant isolation.  
**Aligned to:** `Xenasys_Database_Design.md`, `Xenasys_Task_Matrix.md` (X.5), `Xenasys_API_Specification.md`  
**Client library (planned):** `StackExchange.Redis` on .NET · optional read-through from Next.js only via API (browser never talks to Redis directly)

---

## 1. Design principles

1. **Cache is a performance layer, not a security boundary.** Every miss path hits EF with JWT `company_id` filters.  
2. **Tenant is always in the key.** No global product/category keys without `company:{id}`.  
3. **Short TTL + explicit invalidation** on mutations (prefer correctness over stale catalogs).  
4. **Do not cache** per-user secrets, password hashes, or full JWT material.  
5. **Hot-path lists** may cache the **first keyset page** only; deep cursors usually bypass or use separate cursor keys.  
6. **Invalidation is part of the write transaction’s after-commit hook** (or outbox), never “best effort forgotten.”

---

## 2. Environment & connection

| Setting | Guidance |
|---------|----------|
| Instance | Redis 7.x (Cluster later if needed) |
| DB index | `0` app cache (reserve others for sessions if split) |
| TLS | Required in non-dev |
| Timeout | Connect ≤ 5s; sync timeout ≤ 500ms — on failure **fail open to DB** |
| Serializer | JSON (System.Text.Json) camelCase matching API DTOs |
| Compression | Optional for payloads > 10KB |

**Config keys (Presentation):**

```text
ConnectionStrings:Redis = localhost:6379
Caching:DefaultTtlSeconds = 300
Caching:MasterDataTtlSeconds = 1800
Caching:KeyPrefix = xenasys
```

---

## 3. Key namespace grammar

```text
{prefix}:{version}:c:{companyId}:{domain}:{descriptor}
```

| Segment | Example | Notes |
|---------|---------|--------|
| `prefix` | `xenasys` | Env isolation (`xenasys:dev`) |
| `version` | `v1` | Bump to global flush schema |
| `c:{companyId}` | `c:8f2a…` | **Mandatory** for tenant data |
| `domain` | `cat`, `brand`, `prod`, `coa`, `unit` | Short domain codes |
| `descriptor` | `list:active`, `byId:{id}`, `page:first:50` | Stable, predictable |

**Platform (non-tenant) masters:**

```text
{prefix}:{version}:platform:{domain}:{descriptor}
```

Example: units, master_accounts (read-mostly).

---

## 4. Catalog & COA key map

### 4.1 Units (platform)

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:platform:unit:list:active` | `UnitDto[]` | 24h | Unit admin mutation (rare) |

### 4.2 Categories (tenant)

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:cat:list:active` | `CategoryDto[]` (dropdown) | 30 min | Category create/update/deactivate/delete |
| `xenasys:v1:c:{cid}:cat:byId:{id}` | `CategoryDto` | 30 min | That category mutation |
| `xenasys:v1:c:{cid}:cat:exists:{nameHash}` | `0\|1` | 5 min | Category rename/create (name uniqueness) |

`nameHash` = uppercase invariant hash of normalized name (do not store raw PII-heavy strings if avoidable).

### 4.3 Sub-categories (tenant)

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:subcat:byCat:{categoryId}` | `SubCategoryDto[]` | 30 min | Sub-cat mutation **or** parent category deactivate |
| `xenasys:v1:c:{cid}:subcat:byId:{id}` | `SubCategoryDto` | 30 min | That sub-cat mutation |

### 4.4 Brands (tenant)

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:brand:list:active` | `BrandDto[]` | 30 min | Brand mutation |
| `xenasys:v1:c:{cid}:brand:byId:{id}` | `BrandDto` | 30 min | That brand mutation |

### 4.5 Products (tenant) — careful

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:prod:byId:{id}` | `ProductDto` | 5–15 min | Product update/active/stock affecting DTO |
| `xenasys:v1:c:{cid}:prod:sku:{sku}` | `productId` or DTO | 15 min | SKU immutable post-create → invalidate on delete/deactivate |
| `xenasys:v1:c:{cid}:prod:page:first:{limit}` | First keyset page JSON | 30–60s | **Any** product create/update/delete/active for company |

**Do not** cache arbitrary `afterId` pages in v1 (key cardinality explosion). Deep pages hit DB keyset index.

### 4.6 Vendors (tenant)

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:vendor:byId:{id}` | `VendorDto` | 15 min | Vendor mutation |
| `xenasys:v1:c:{cid}:vendor:ntn:{ntn}` | `vendorId` | 15 min | Vendor NTN change (rare) / create |
| `xenasys:v1:c:{cid}:vendor:page:first:{limit}` | First page | 30–60s | Vendor create/update/delete |

### 4.7 COA

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:platform:coa:master:list` | Master accounts | 1–24h | Super Admin master write |
| `xenasys:v1:platform:coa:control:byKind:{kind}` | Control id/DTO (`INVENTORY`, `ACCOUNTS_PAYABLE`) | 1–24h | Control seed/admin change |
| `xenasys:v1:c:{cid}:coa:sub:byId:{id}` | Subsidiary DTO | 15 min | Subsidiary create (product/vendor TX) / deactivate |
| `xenasys:v1:c:{cid}:coa:sub:page:first:{limit}` | First page | 30–60s | Subsidiary insert |

### 4.8 Tenant profile

| Key | Value | TTL | Invalidate on |
|-----|-------|-----|----------------|
| `xenasys:v1:c:{cid}:tenant:profile` | `TenantDto` | 30 min | Tenant profile update |

---

## 5. Real-time invalidation rules

### 5.1 Strategy

**Primary:** targeted `DEL` / `UNLINK` of known keys after successful **DB commit**.  
**Secondary:** version stamp (optional later):

```text
xenasys:v1:c:{cid}:cat:ver  → integer
```

List keys include `:ver:{n}`; bump version on mutation to soft-invalidate without enumerating keys.

### 5.2 Mutation → invalidate matrix

| Mutation | Keys to invalidate (minimum) |
|----------|------------------------------|
| Category CUD | `cat:list:active`, `cat:byId:{id}`, `cat:exists:*` (or bump cat ver), all `subcat:byCat:{id}` if category id affected |
| Sub-category CUD | `subcat:byCat:{categoryId}`, `subcat:byId:{id}`, `prod:page:first:*` (dropdown freshness) |
| Brand CUD | `brand:list:active`, `brand:byId:{id}`, `prod:page:first:*` |
| Product create (TX + subsidiary) | `prod:page:first:*`, `prod:byId:{id}`, `prod:sku:{sku}`, `coa:sub:page:first:*`, `coa:sub:byId:{subId}` |
| Product update / active toggle | `prod:byId:{id}`, `prod:page:first:*`, `prod:sku:{sku}` if returned in lists |
| Product stock change | `prod:byId:{id}` if qty in DTO; else skip list cache |
| Vendor create (TX + AP sub) | `vendor:page:first:*`, `vendor:byId`, `vendor:ntn:`, `coa:sub:*` first page + byId |
| Master/Control COA admin | `platform:coa:master:list`, `platform:coa:control:byKind:*` |
| Tenant profile update | `tenant:profile` |

### 5.3 Implementation sketch (.NET)

```csharp
// After Commit successfully:
await cache.RemoveAsync($"xenasys:v1:c:{companyId}:prod:page:first:50", ct);
await cache.RemoveAsync($"xenasys:v1:c:{companyId}:prod:byId:{productId}", ct);
// Prefer ICacheInvalidator per aggregate to avoid scattering key strings
```

Register invalidation in the **same application service** that performs the write — not only in the controller.

### 5.4 Pub/sub (multi-instance)

When multiple API nodes run:

1. Writer deletes keys in Redis (shared).  
2. Optional: publish `cache:invalidate` channel with `{ companyId, tags: ["prod","coa-sub"] }` so in-memory L1 (if any) drops.

**v1:** Redis-only (no local L1) keeps invalidation simple.

---

## 6. Read path algorithm

```text
1. Build tenant-scoped key from JWT company_id (never from client).
2. GET Redis → hit? return DTO.
3. Miss → query EF (global tenant filter on) → SET key with TTL → return.
4. Redis errors → log + DB only (fail open).
```

---

## 7. What not to cache (v1)

- Full unbounded product catalogs  
- Arbitrary keyset pages after the first  
- Auth tokens / refresh tokens  
- Super Admin cross-tenant raw dumps (use rollup tables)  
- In-flight validation “exists” checks without short TTL (stampede risk) — use single-flight / lock if needed  

---

## 8. Observability

| Metric | Alert |
|--------|-------|
| Hit ratio per domain (`cat`, `prod`) | Sustained < 40% on cat/brand → investigate TTL/invalidation |
| Redis latency p99 | > 20ms |
| Fail-open count | Spike → Redis health |
| Invalidation lag | Writes without matching DEL |

---

## 9. Phase 0 readiness

| Item | Phase 0 action |
|------|----------------|
| Connection string placeholder | Add to `appsettings` (optional unused until M2) |
| `ICacheService` / `ICacheInvalidator` abstractions | Core + Infrastructure stub OK after M0 auth |
| First real keys | Categories/brands at **M2**; product first-page at **M3** |
| Browser | No direct Redis |

---

## 10. Conflict notice (Task Matrix)

Caching must **never**:

- Skip EF tenant filters on miss fill  
- Serve Company A data under Company B keys (key construction bugs = Sev-0)  
- Bypass Product/Vendor COA transactional writes for “cache-only” creates  

Pre-check against Tasks **0.2**, **3.1**, **4.1**, **X.5** before implementing cache middleware.

---

*End of Redis Cache Architecture.*
