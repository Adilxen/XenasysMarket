# Xenasys API Specification — JSON Envelopes & Status Contracts

**Audience:** Frontend (Next.js), Backend (.NET Presentation), QA  
**Aligned to:** BRD §2 (422 field errors), `Xenasys_UI_Validation_Plan.md`, `Xenasys_Task_Matrix.md`  
**Content-Type:** `application/json; charset=utf-8`  
**Property naming:** **camelCase** in JSON (ASP.NET default System.Text.Json)

---

## 1. Goals

- One predictable wire format for success and failure across all modules (M0–M7).
- Field-level validation errors must map 1:1 onto React Hook Form inputs.
- Auth failures must be distinguishable from validation failures (`401` vs `422`).
- Never leak cross-tenant data or stack traces in production envelopes.

---

## 2. Success envelope

### 2.1 Single resource

**HTTP:** `200 OK` (read/update) · `201 Created` (create; include `Location` when applicable)

```json
{
  "success": true,
  "data": {
    "id": "11111111-1111-1111-1111-111111111111",
    "productName": "Widget Box",
    "sku": "CAT01-SUB04-PROD88-BOX12-PCS"
  },
  "meta": {
    "requestId": "b3a0f2c1-6e2a-4c8d-9f1e-2a7b8c9d0e1f",
    "timestamp": "2026-09-10T19:30:00.000Z"
  }
}
```

### 2.2 Keyset list (hot path — no OFFSET)

**HTTP:** `200 OK`

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1",
        "sku": "CAT01-SUB01-ITEM01-BOX-PCS",
        "productName": "Sample"
      }
    ],
    "nextCursor": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1",
    "hasMore": true
  },
  "meta": {
    "requestId": "…",
    "timestamp": "…",
    "limit": 50
  }
}
```

**Query contract:**

| Param | Type | Notes |
|-------|------|--------|
| `afterId` | uuid \| omit | Exclusive cursor; omit for first page |
| `limit` | int | Default 50; server-capped (e.g. max 100) |

### 2.3 Empty success

**HTTP:** `204 No Content` for deletes with no body **or** `200` with:

```json
{
  "success": true,
  "data": null,
  "meta": { "requestId": "…", "timestamp": "…" }
}
```

Prefer consistent `200` + envelope for SPA simplicity unless DELETE is bodyless by convention.

### 2.4 Login success (M0)

**HTTP:** `200 OK`

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "userId": "…",
    "tenantId": "…",
    "role": "TENANT_ADMIN",
    "userName": "amjad"
  },
  "meta": {
    "requestId": "…",
    "timestamp": "…"
  }
}
```

JWT claims (inside token, not necessarily duplicated beyond `data`): `user_id`, `tenant_id`, `role`.

---

## 3. Validation failure — HTTP 422 Unprocessable Entity

BRD: structured **field-level** errors. Frontend maps via `mapApiErrorsToForm`.

### 3.1 Canonical envelope

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_FAILED",
    "message": "One or more validation errors occurred.",
    "fields": {
      "userName": ["User Name is required."],
      "password": ["Password must be at least 8 characters."],
      "companyId": ["Company selection is required."]
    }
  },
  "meta": {
    "requestId": "…",
    "timestamp": "…"
  }
}
```

### 3.2 ASP.NET compatibility shape (also accepted by client)

Controllers may emit ProblemDetails-compatible payloads; the UI mapper accepts **either** `error.fields` **or** top-level `errors`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "retailPrice": [
      "Retail Price must be greater than or equal to Invoice Price."
    ],
    "ntnNo": [
      "NTN No must match format 1234567-8."
    ]
  },
  "traceId": "…"
}
```

**Rules:**

- Keys = **camelCase** field names matching RHF / DTO names (`retailPrice`, not only `RetailPrice` — normalize on client if PascalCase received).
- Messages = exact UX copy from `Xenasys_UI_Validation_Plan.md` where applicable.
- First message per field is shown under the input (`#DC2626`).

### 3.3 Typical 422 cases

| Scenario | Example field keys |
|----------|-------------------|
| Login missing company | `companyId` |
| Price guard | `retailPrice` |
| NTN mask | `ntnNo` |
| Category duplicate name | `name` |
| FK dropdown invalid | `categoryId`, `unitId` |

---

## 4. Unauthorized — HTTP 401

### 4.1 Envelope

```json
{
  "success": false,
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Authentication is required or the session is invalid.",
    "fields": null
  },
  "meta": {
    "requestId": "…",
    "timestamp": "…"
  }
}
```

### 4.2 When to return 401

- Missing / malformed `Authorization: Bearer` token  
- Expired or invalid JWT signature  
- Login credentials invalid (optional: use 401 with `code: "INVALID_CREDENTIALS"` — **do not** reveal which of username/password failed)

```json
{
  "success": false,
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "Invalid user name or password.",
    "fields": null
  },
  "meta": { "requestId": "…", "timestamp": "…" }
}
```

**Frontend:** clear session storage and redirect to `/login` (existing Axios interceptor pattern).

---

## 5. Forbidden — HTTP 403 (related)

```json
{
  "success": false,
  "error": {
    "code": "FORBIDDEN",
    "message": "You do not have permission to perform this action.",
    "fields": null
  },
  "meta": { "requestId": "…", "timestamp": "…" }
}
```

Use for RBAC failures and cross-tenant access attempts (after auth succeeded).

---

## 6. Not found — HTTP 404

```json
{
  "success": false,
  "error": {
    "code": "NOT_FOUND",
    "message": "The requested resource was not found.",
    "fields": null
  },
  "meta": { "requestId": "…", "timestamp": "…" }
}
```

Tenant isolation: resources in another tenant return **404** (not 403) to avoid existence leaks, unless Super Admin context intentionally queries cross-tenant.

---

## 7. Conflict / business rule — HTTP 409 (optional)

```json
{
  "success": false,
  "error": {
    "code": "CONFLICT",
    "message": "System-generated subsidiary accounts cannot be deleted.",
    "fields": null
  },
  "meta": { "requestId": "…", "timestamp": "…" }
}
```

---

## 8. Envelope TypeScript contracts (frontend)

```ts
export interface ApiMeta {
  requestId: string;
  timestamp: string;
  limit?: number;
}

export interface ApiSuccess<T> {
  success: true;
  data: T;
  meta: ApiMeta;
}

export interface ApiErrorBody {
  code: string;
  message: string;
  fields: Record<string, string[]> | null;
}

export interface ApiFailure {
  success: false;
  error: ApiErrorBody;
  meta: ApiMeta;
}

export type ApiResponse<T> = ApiSuccess<T> | ApiFailure;

export interface KeysetPage<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}
```

---

## 9. .NET implementation notes (Phase 0+)

- Prefer a single `IActionResult` filter or result wrapper that shapes success/error consistently.
- Validation: FluentValidation or DataAnnotations → map to **422** + `fields`.
- `requestId`: use `HttpContext.TraceIdentifier` or Activity id.
- `timestamp`: UTC ISO-8601.
- Production: no exception messages or SQL text inside `error.message`.

---

## 10. Status code cheat sheet

| Code | Meaning | Envelope |
|------|---------|----------|
| 200 | OK | `success: true` + `data` |
| 201 | Created | `success: true` + `data` |
| 204 | No content | empty body (optional) |
| 401 | Auth required / invalid session / bad login | `UNAUTHORIZED` / `INVALID_CREDENTIALS` |
| 403 | Authenticated but not allowed | `FORBIDDEN` |
| 404 | Missing **or** other-tenant hide | `NOT_FOUND` |
| 409 | Business conflict | `CONFLICT` |
| 422 | Field validation | `VALIDATION_FAILED` + `fields` |
| 500 | Unexpected | generic message only |

---

*Phase 0 should adopt these envelopes on Auth and Tenant endpoints first, then all subsequent modules.*
