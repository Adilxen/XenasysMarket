import type {
  CreateControlAccountRequest,
  CreateMasterAccountRequest,
  CreateSubsidiaryAccountRequest,
  CreateTenantRequest,
  CreateBrandRequest,
  CreateVendorRequest,
  LoginCompanyChallenge,
  LoginRequest,
  LoginResponse,
  LoginResult,
  ControlAccountDto,
  MasterAccountDto,
  ProductDto,
  SubsidiaryAccountDto,
  TenantDto,
  UpdateBrandRequest,
  UpdateControlAccountRequest,
  UpdateMasterAccountRequest,
  UpdateSubsidiaryAccountRequest,
  UpdateTenantProfileRequest,
  UpdateTenantRequest,
  UpdateVendorRequest,
  VendorDto,
  StockItemDto,
  StockAlertDto,
  StockMovementDto,
  UpdateStockRequest,
  DashboardDto,
  DashboardInventoryRowDto,
  KeysetPageRequest,
  KeysetPageResult,
} from "@/types/api";
import { isLoginChallenge } from "@/types/api";
import type { LoginTenantOption } from "@/types/auth";
import type {
  BrandDto,
  CategoryDto,
  CategoryOptionDto,
  SetProductActiveRequest,
  SubCategoryDto,
  UnitDto,
} from "@/types/catalog";
import type {
  CreateSalesOrderRequest,
  SalesOrderDto,
  UpdateSalesOrderRequest,
} from "@/types/sales";
import type {
  ActionDto,
  AddUserMembershipRequest,
  CreateActionRequest,
  CreateCustomRoleRequest,
  CreateFormRequest,
  CreateRoleRequest,
  CreateTenantUserRequest,
  CurrentUserPermissionsDto,
  CurrentUserProfileDto,
  FormActionAssignmentDto,
  FormActionAuditMatrixDto,
  FormDto,
  FormWithActionsDto,
  RoleDto,
  RoleFormActionsDto,
  SyncFormActionsRequest,
  SyncRoleFormActionsRequest,
  TenantUserDto,
  UpdateActionRequest,
  UpdateCurrentUserProfileRequest,
  UpdateFormRequest,
  UpdateRoleRequest,
  UpdateTenantUserRequest,
  UpdateUserMembershipRequest,
} from "@/types/rbac";
import type { GlobalSearchResponseDto } from "@/types/search";
import { apiDelete, apiGet, apiPost, apiPut, apiClient } from "@/lib/api/client";
import { fetchKeysetPage } from "@/lib/api/pagination";
import { appendColumnFilters } from "@/hooks/use-server-column-filters";
import { sessionStore } from "@/lib/auth/session";
import { invalidateMyPermissionsCache } from "@/lib/auth/use-my-permissions";
/**
 * ==========================================================================================
 * WHAT: Fetches tenant/company options for the login company dropdown (id + name).
 * WHY: UI shows company name; RHF+Zod requires company_id INT for the login POST payload.
 * EXAMPLE: listLoginCompanies() → [{ id: 12, name: "Acme", tenantCode: "ACME" }, ...].
 * ==========================================================================================
 */
export async function listLoginCompanies(): Promise<LoginTenantOption[]> {
  return apiGet<LoginTenantOption[]>("/api/auth/companies");
}

export async function getMyProfile(): Promise<CurrentUserProfileDto> {
  return apiGet<CurrentUserProfileDto>("/api/auth/me");
}

export async function updateMyProfile(
  body: UpdateCurrentUserProfileRequest,
): Promise<CurrentUserProfileDto> {
  return apiPut<UpdateCurrentUserProfileRequest, CurrentUserProfileDto>(
    "/api/auth/me",
    body,
  );
}

/**
 * ==========================================================================================
 * WHAT: POSTs LoginRequest (userName, password, company_id INT) and stores JWT on success.
 * WHY: Completes RHF+Zod-validated login; challenge response means re-select company_id.
 * EXAMPLE: login({ userName, password, company_id: 12 }) → session + /dashboard, or challenge.
 * ==========================================================================================
 */
export async function login(request: LoginRequest): Promise<LoginResult> {
  const response = await apiPost<LoginRequest, LoginResult>(
    "/api/auth/login",
    request,
  );

  if (!isLoginChallenge(response)) {
    invalidateMyPermissionsCache();
    sessionStore.setSession(
      response.accessToken ?? response.token,
      String(response.tenantId),
      response.role,
      response.userName,
      response.userName,
      response.companyName,
    );
  }

  return response;
}

export type { LoginCompanyChallenge, LoginResponse };

export async function getCurrentTenant(): Promise<TenantDto> {
  return apiGet<TenantDto>("/api/tenant/me");
}

export async function updateCurrentTenant(
  body: UpdateTenantProfileRequest,
): Promise<TenantDto> {
  return apiPut<UpdateTenantProfileRequest, TenantDto>("/api/tenant/me", body);
}

/** Settings → Company listing (Super Admin = all; Tenant Admin = JWT company). */
export async function listTenants(options?: {
  includeInactive?: boolean;
  shape?: "list" | "detail";
  filters?: Record<string, string>;
}): Promise<TenantDto[]> {
  const params = new URLSearchParams();
  if (options?.includeInactive) params.set("includeInactive", "true");
  params.set("shape", options?.shape ?? "list");
  appendColumnFilters(params, options?.filters);
  const qs = params.toString();
  return apiGet<TenantDto[]>(`/api/tenants${qs ? `?${qs}` : ""}`);
}

/** Single company — default shape=detail (phone/address) without SELECT *. */
export async function getTenant(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<TenantDto> {
  return apiGet<TenantDto>(`/api/tenants/${id}?shape=${shape}`);
}

export async function createTenant(body: CreateTenantRequest): Promise<TenantDto> {
  return apiPost<CreateTenantRequest, TenantDto>("/api/tenants", body);
}

export async function updateTenant(
  id: number,
  body: UpdateTenantRequest,
): Promise<TenantDto> {
  return apiPut<UpdateTenantRequest, TenantDto>(`/api/tenants/${id}`, body);
}

/** Soft-deactivate company (IsActive = false). Super Admin only. */
export async function deactivateTenant(id: number): Promise<void> {
  await apiDelete<void>(`/api/tenants/${id}`);
}

/** Chart of Accounts → Master Account listing (platform-shared keyset). */
export async function listMasterAccounts(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<MasterAccountDto>> {
  return fetchKeysetPage<MasterAccountDto>("/api/coa/masters", request);
}

export async function getMasterAccount(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<MasterAccountDto> {
  return apiGet<MasterAccountDto>(`/api/coa/masters/${id}?shape=${shape}`);
}

export async function createMasterAccount(
  body: CreateMasterAccountRequest,
): Promise<MasterAccountDto> {
  return apiPost<CreateMasterAccountRequest, MasterAccountDto>("/api/coa/masters", body);
}

export async function updateMasterAccount(
  id: number,
  body: UpdateMasterAccountRequest,
): Promise<MasterAccountDto> {
  return apiPut<UpdateMasterAccountRequest, MasterAccountDto>(`/api/coa/masters/${id}`, body);
}

/** Soft-deactivate master account (IsActive = false). Super Admin only. */
export async function deactivateMasterAccount(id: number): Promise<void> {
  await apiDelete<void>(`/api/coa/masters/${id}`);
}

/** Chart of Accounts → Control Account listing (tenant-scoped keyset). */
export async function listControlAccounts(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<ControlAccountDto>> {
  return fetchKeysetPage<ControlAccountDto>("/api/coa/controls", request);
}

export async function getControlAccount(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<ControlAccountDto> {
  return apiGet<ControlAccountDto>(`/api/coa/controls/${id}?shape=${shape}`);
}

export async function createControlAccount(
  body: CreateControlAccountRequest,
): Promise<ControlAccountDto> {
  return apiPost<CreateControlAccountRequest, ControlAccountDto>("/api/coa/controls", body);
}

export async function updateControlAccount(
  id: number,
  body: UpdateControlAccountRequest,
): Promise<ControlAccountDto> {
  return apiPut<UpdateControlAccountRequest, ControlAccountDto>(`/api/coa/controls/${id}`, body);
}

/** Soft-deactivate control account (OTHER only). Super Admin only. */
export async function deactivateControlAccount(id: number): Promise<void> {
  await apiDelete<void>(`/api/coa/controls/${id}`);
}

/** Subsidiary Accounts — tenant-scoped keyset list. */
export async function listSubsidiaryAccounts(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<SubsidiaryAccountDto>> {
  return fetchKeysetPage<SubsidiaryAccountDto>("/api/coa/subsidiaries", request);
}

export async function getSubsidiaryAccount(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<SubsidiaryAccountDto> {
  return apiGet<SubsidiaryAccountDto>(`/api/coa/subsidiaries/${id}?shape=${shape}`);
}

export async function createSubsidiaryAccount(
  body: CreateSubsidiaryAccountRequest,
): Promise<SubsidiaryAccountDto> {
  return apiPost<CreateSubsidiaryAccountRequest, SubsidiaryAccountDto>(
    "/api/coa/subsidiaries",
    body,
  );
}

export async function updateSubsidiaryAccount(
  id: number,
  body: UpdateSubsidiaryAccountRequest,
): Promise<SubsidiaryAccountDto> {
  return apiPut<UpdateSubsidiaryAccountRequest, SubsidiaryAccountDto>(
    `/api/coa/subsidiaries/${id}`,
    body,
  );
}

/** Soft-deactivate manual subsidiary (blocked when isSystemGenerated). */
export async function deactivateSubsidiaryAccount(id: number): Promise<void> {
  await apiDelete<void>(`/api/coa/subsidiaries/${id}`);
}

export async function listProducts(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<ProductDto>> {
  return fetchKeysetPage<ProductDto>("/api/products", request);
}

export async function getProduct(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<ProductDto> {
  return apiGet<ProductDto>(`/api/products/${id}?shape=${shape}`);
}

export async function createProduct(form: FormData): Promise<ProductDto> {
  const { data } = await apiClient.post<ProductDto>("/api/products", form, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data;
}

export async function updateProduct(
  id: number,
  form: FormData,
): Promise<ProductDto> {
  const { data } = await apiClient.put<ProductDto>(`/api/products/${id}`, form, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data;
}

/** Soft-deactivate product (IsActive=false); inventory subsidiary retained. */
export async function deactivateProduct(id: number): Promise<void> {
  await apiDelete<void>(`/api/products/${id}`);
}

export async function setProductActive(
  id: number,
  body: SetProductActiveRequest,
): Promise<ProductDto> {
  return apiPut<SetProductActiveRequest, ProductDto>(
    `/api/products/${id}/active`,
    body,
  );
}

/** M5 Stock — balances, update qty, low-stock alerts, movements. */
export async function listStock(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<StockItemDto>> {
  return fetchKeysetPage<StockItemDto>("/api/stock", request);
}

export async function getStockItem(productId: number): Promise<StockItemDto> {
  return apiGet<StockItemDto>(`/api/stock/${productId}`);
}

export async function updateStock(
  productId: number,
  body: UpdateStockRequest,
): Promise<StockItemDto> {
  return apiPut<UpdateStockRequest, StockItemDto>(
    `/api/stock/${productId}`,
    body,
  );
}

export async function listStockAlerts(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<StockAlertDto>> {
  return fetchKeysetPage<StockAlertDto>("/api/stock/alerts", request);
}

/** Mark a pending low-stock outbox row as Processed (header Notification Center). */
export async function acknowledgeStockAlert(outboxId: number): Promise<void> {
  await apiPost<Record<string, never>, void>(
    `/api/stock/alerts/${outboxId}/ack`,
    {},
  );
}

export async function listStockMovements(
  productId: number,
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<StockMovementDto>> {
  return fetchKeysetPage<StockMovementDto>(
    `/api/stock/${productId}/movements`,
    request,
  );
}

/** Unified production Dashboard. */
export async function getDashboard(options?: {
  companyId?: number;
  companyName?: string;
  includeEmpty?: boolean;
  afterId?: number;
  limit?: number;
}): Promise<DashboardDto> {
  const params = new URLSearchParams();
  if (options?.companyId != null && options.companyId > 0) {
    params.set("companyId", String(options.companyId));
  }
  if (options?.companyName?.trim()) {
    params.set("companyName", options.companyName.trim());
  }
  if (options?.includeEmpty) params.set("includeEmpty", "true");
  if (options?.afterId != null) params.set("afterId", String(options.afterId));
  if (options?.limit != null) params.set("limit", String(options.limit));
  const qs = params.toString();
  return apiGet<DashboardDto>(`/api/dashboard${qs ? `?${qs}` : ""}`);
}

/** @deprecated Prefer getDashboard */
export async function getSuperAdminDashboard(
  companyId?: number,
): Promise<DashboardDto> {
  return getDashboard({ companyId });
}

export async function getSuperAdminInventorySummary(
  companyId?: number,
): Promise<DashboardInventoryRowDto[]> {
  const qs =
    companyId != null && companyId > 0 ? `?companyId=${companyId}` : "";
  return apiGet<DashboardInventoryRowDto[]>(
    `/api/super/inventory-summary${qs}`,
  );
}

export async function getSuperAdminSalesByTenant(
  companyId?: number,
): Promise<
  Array<{
    companyId: number;
    companyName: string;
    tenantCode: string;
    grossSales: number;
    orderCount: number;
    status: string;
  }>
> {
  const qs =
    companyId != null && companyId > 0 ? `?companyId=${companyId}` : "";
  return apiGet(`/api/super/sales-by-tenant${qs}`);
}

/** Vendors — tenant-scoped keyset list; AP subsidiary created on POST. */
export async function listVendors(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<VendorDto>> {
  return fetchKeysetPage<VendorDto>("/api/vendors", request);
}

export async function getVendor(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<VendorDto> {
  return apiGet<VendorDto>(`/api/vendors/${id}?shape=${shape}`);
}

export async function createVendor(body: CreateVendorRequest): Promise<VendorDto> {
  return apiPost<CreateVendorRequest, VendorDto>("/api/vendors", body);
}

export async function updateVendor(
  id: number,
  body: UpdateVendorRequest,
): Promise<VendorDto> {
  return apiPut<UpdateVendorRequest, VendorDto>(`/api/vendors/${id}`, body);
}

/** Soft-deactivate vendor; linked AP subsidiary remains for ledger trail. */
export async function deactivateVendor(id: number): Promise<void> {
  await apiDelete<void>(`/api/vendors/${id}`);
}

/** Absolute URL for catalog images stored under API wwwroot. */
export function catalogImageUrl(imagePath: string | null | undefined): string | null {
  if (!imagePath?.trim() || imagePath.trim() === "/" || imagePath.trim() === "\\") {
    return null;
  }
  if (/^https?:\/\//i.test(imagePath)) return imagePath;
  const base =
    process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5077";
  return `${base.replace(/\/$/, "")}${imagePath.startsWith("/") ? "" : "/"}${imagePath}`;
}

/** Categories — keyset list; multipart create/update with mandatory image. */
export async function listCategoriesPage(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<CategoryDto>> {
  return fetchKeysetPage<CategoryDto>("/api/categories", request);
}

/** Flattened active categories for product dropdowns (id + name). */
export async function listCategories(): Promise<CategoryOptionDto[]> {
  const page = await listCategoriesPage({ limit: 200, shape: "list" });
  return page.items.map((c) => ({ id: c.id, name: c.name }));
}

export async function getCategory(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<CategoryDto> {
  return apiGet<CategoryDto>(`/api/categories/${id}?shape=${shape}`);
}

export async function createCategory(form: FormData): Promise<CategoryDto> {
  const { data } = await apiClient.post<CategoryDto>("/api/categories", form, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data;
}

export async function updateCategory(
  id: number,
  form: FormData,
): Promise<CategoryDto> {
  const { data } = await apiClient.put<CategoryDto>(
    `/api/categories/${id}`,
    form,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return data;
}

export async function deactivateCategory(id: number): Promise<void> {
  await apiDelete<void>(`/api/categories/${id}`);
}

/** Sub-categories — optional categoryId filter for cascading dropdowns. */
export async function listSubCategories(
  request: KeysetPageRequest & { categoryId?: number } = {},
): Promise<KeysetPageResult<SubCategoryDto>> {
  const params = new URLSearchParams();
  if (request.afterId != null) params.set("afterId", String(request.afterId));
  params.set("limit", String(request.limit ?? 50));
  if (request.includeInactive) params.set("includeInactive", "true");
  if (request.shape) params.set("shape", request.shape);
  if (request.categoryId != null) params.set("categoryId", String(request.categoryId));
  if (request.companyId != null && Number(request.companyId) > 0) {
    params.set("companyId", String(request.companyId));
  }
  appendColumnFilters(params, request.filters);
  const qs = params.toString();
  return apiGet<KeysetPageResult<SubCategoryDto>>(
    `/api/sub-categories${qs ? `?${qs}` : ""}`,
  );
}

export async function getSubCategory(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<SubCategoryDto> {
  return apiGet<SubCategoryDto>(`/api/sub-categories/${id}?shape=${shape}`);
}

export async function createSubCategory(form: FormData): Promise<SubCategoryDto> {
  const { data } = await apiClient.post<SubCategoryDto>(
    "/api/sub-categories",
    form,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return data;
}

export async function updateSubCategory(
  id: number,
  form: FormData,
): Promise<SubCategoryDto> {
  const { data } = await apiClient.put<SubCategoryDto>(
    `/api/sub-categories/${id}`,
    form,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return data;
}

export async function deactivateSubCategory(id: number): Promise<void> {
  await apiDelete<void>(`/api/sub-categories/${id}`);
}

/** Brands — tenant-scoped keyset CRUD. */
export async function listBrands(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<BrandDto>> {
  return fetchKeysetPage<BrandDto>("/api/brands", request);
}

export async function getBrand(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<BrandDto> {
  return apiGet<BrandDto>(`/api/brands/${id}?shape=${shape}`);
}

export async function createBrand(body: CreateBrandRequest): Promise<BrandDto> {
  return apiPost<CreateBrandRequest, BrandDto>("/api/brands", body);
}

export async function updateBrand(
  id: number,
  body: UpdateBrandRequest,
): Promise<BrandDto> {
  return apiPut<UpdateBrandRequest, BrandDto>(`/api/brands/${id}`, body);
}

export async function deactivateBrand(id: number): Promise<void> {
  await apiDelete<void>(`/api/brands/${id}`);
}

/** Sales Orders — Phase-1 ledger (T8.1). */
export async function listSalesOrders(
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<SalesOrderDto>> {
  return fetchKeysetPage<SalesOrderDto>("/api/sales-orders", request);
}

export async function getSalesOrder(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<SalesOrderDto> {
  return apiGet<SalesOrderDto>(`/api/sales-orders/${id}?shape=${shape}`);
}

export async function createSalesOrder(
  body: CreateSalesOrderRequest,
): Promise<SalesOrderDto> {
  return apiPost<CreateSalesOrderRequest, SalesOrderDto>(
    "/api/sales-orders",
    body,
  );
}

export async function updateSalesOrder(
  id: number,
  body: UpdateSalesOrderRequest,
): Promise<SalesOrderDto> {
  return apiPut<UpdateSalesOrderRequest, SalesOrderDto>(
    `/api/sales-orders/${id}`,
    body,
  );
}

export async function cancelSalesOrder(id: number): Promise<void> {
  await apiDelete<void>(`/api/sales-orders/${id}`);
}

/** Platform Units — system UoM seed (Pcs/Kg/Ltr/Box/Pack). */
export async function listUnits(includeInactive = false): Promise<UnitDto[]> {
  const q = includeInactive ? "?includeInactive=true" : "";
  return apiGet<UnitDto[]>(`/api/units${q}`);
}

export async function listRoles(opts?: {
  shape?: "list" | "detail";
  includeInactive?: boolean;
  filters?: Record<string, string>;
}): Promise<RoleDto[]> {
  const q = new URLSearchParams();
  if (opts?.shape) q.set("shape", opts.shape);
  if (opts?.includeInactive) q.set("includeInactive", "true");
  appendColumnFilters(q, opts?.filters);
  const qs = q.toString();
  return apiGet<RoleDto[]>(`/api/roles${qs ? `?${qs}` : ""}`);
}

export async function createRole(body: CreateRoleRequest): Promise<RoleDto> {
  return apiPost<CreateRoleRequest, RoleDto>("/api/roles", body);
}

/** @deprecated Prefer createRole — Role Code is auto-allocated. */
export async function createCustomRole(
  body: CreateCustomRoleRequest,
): Promise<RoleDto> {
  return createRole({
    name: body.name,
  });
}

export async function getRole(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<RoleDto> {
  return apiGet<RoleDto>(`/api/roles/${id}?shape=${shape}`);
}

export async function updateRole(
  id: number,
  body: UpdateRoleRequest,
): Promise<RoleDto> {
  return apiPut<UpdateRoleRequest, RoleDto>(`/api/roles/${id}`, body);
}

export async function deactivateRole(id: number): Promise<void> {
  await apiDelete(`/api/roles/${id}`);
}

export async function listActions(opts?: {
  shape?: "list" | "detail";
  includeInactive?: boolean;
  filters?: Record<string, string>;
}): Promise<ActionDto[]> {
  const q = new URLSearchParams();
  if (opts?.shape) q.set("shape", opts.shape);
  if (opts?.includeInactive) q.set("includeInactive", "true");
  appendColumnFilters(q, opts?.filters);
  const qs = q.toString();
  return apiGet<ActionDto[]>(`/api/actions${qs ? `?${qs}` : ""}`);
}

export async function getAction(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<ActionDto> {
  return apiGet<ActionDto>(`/api/actions/${id}?shape=${shape}`);
}

export async function createAction(body: CreateActionRequest): Promise<ActionDto> {
  return apiPost<CreateActionRequest, ActionDto>("/api/actions", body);
}

export async function updateAction(
  id: number,
  body: UpdateActionRequest,
): Promise<ActionDto> {
  return apiPut<UpdateActionRequest, ActionDto>(`/api/actions/${id}`, body);
}

export async function deactivateAction(id: number): Promise<void> {
  await apiDelete(`/api/actions/${id}`);
}

export async function listForms(opts?: {
  shape?: "list" | "detail";
  includeInactive?: boolean;
  filters?: Record<string, string>;
}): Promise<FormDto[]> {
  const q = new URLSearchParams();
  if (opts?.shape) q.set("shape", opts.shape);
  if (opts?.includeInactive) q.set("includeInactive", "true");
  appendColumnFilters(q, opts?.filters);
  const qs = q.toString();
  return apiGet<FormDto[]>(`/api/forms${qs ? `?${qs}` : ""}`);
}

export async function getForm(
  id: number,
  shape: "list" | "detail" = "detail",
): Promise<FormDto> {
  return apiGet<FormDto>(`/api/forms/${id}?shape=${shape}`);
}

export async function createForm(body: CreateFormRequest): Promise<FormDto> {
  return apiPost<CreateFormRequest, FormDto>("/api/forms", body);
}

export async function updateForm(
  id: number,
  body: UpdateFormRequest,
): Promise<FormDto> {
  return apiPut<UpdateFormRequest, FormDto>(`/api/forms/${id}`, body);
}

export async function deactivateForm(id: number): Promise<void> {
  await apiDelete(`/api/forms/${id}`);
}

export async function listFormActionCatalog(): Promise<FormWithActionsDto[]> {
  return apiGet<FormWithActionsDto[]>("/api/forms/action-catalog");
}

export async function getFormActions(
  formId: number,
): Promise<FormActionAssignmentDto[]> {
  return apiGet<FormActionAssignmentDto[]>(`/api/forms/${formId}/actions`);
}

export async function syncFormActions(
  formId: number,
  body: SyncFormActionsRequest,
): Promise<FormActionAssignmentDto[]> {
  return apiPut<SyncFormActionsRequest, FormActionAssignmentDto[]>(
    `/api/forms/${formId}/actions`,
    body,
  );
}

export async function getRoleFormActions(
  roleId: number,
): Promise<RoleFormActionsDto> {
  return apiGet<RoleFormActionsDto>(`/api/roles/${roleId}/form-actions`);
}

export async function syncRoleFormActions(
  roleId: number,
  body: SyncRoleFormActionsRequest,
): Promise<RoleFormActionsDto> {
  return apiPut<SyncRoleFormActionsRequest, RoleFormActionsDto>(
    `/api/roles/${roleId}/form-actions`,
    body,
  );
}

export async function getFormActionAuditMatrix(opts?: {
  formSearch?: string;
  roleId?: number;
}): Promise<FormActionAuditMatrixDto> {
  const q = new URLSearchParams();
  if (opts?.formSearch?.trim()) q.set("formSearch", opts.formSearch.trim());
  if (opts?.roleId && opts.roleId > 0) q.set("roleId", String(opts.roleId));
  const qs = q.toString();
  return apiGet<FormActionAuditMatrixDto>(
    `/api/permissions/form-action-audit${qs ? `?${qs}` : ""}`,
  );
}

export async function getMyPermissions(): Promise<CurrentUserPermissionsDto> {
  return apiGet<CurrentUserPermissionsDto>("/api/permissions/me");
}

export async function listTenantUsers(opts?: {
  includeInactive?: boolean;
  filters?: Record<string, string>;
}): Promise<TenantUserDto[]> {
  const q = new URLSearchParams();
  if (opts?.includeInactive) q.set("includeInactive", "true");
  appendColumnFilters(q, opts?.filters);
  const qs = q.toString();
  return apiGet<TenantUserDto[]>(`/api/users${qs ? `?${qs}` : ""}`);
}

export async function getTenantUser(userId: number): Promise<TenantUserDto> {
  return apiGet<TenantUserDto>(`/api/users/${userId}`);
}

export async function createTenantUser(
  body: CreateTenantUserRequest,
): Promise<TenantUserDto> {
  return apiPost<CreateTenantUserRequest, TenantUserDto>("/api/users", body);
}

export async function updateTenantUser(
  userId: number,
  body: UpdateTenantUserRequest,
): Promise<TenantUserDto> {
  return apiPut<UpdateTenantUserRequest, TenantUserDto>(
    `/api/users/${userId}`,
    body,
  );
}

/** Super Admin: add another company membership (T1.56). */
export async function addTenantUserMembership(
  userId: number,
  body: AddUserMembershipRequest,
): Promise<TenantUserDto> {
  return apiPost<AddUserMembershipRequest, TenantUserDto>(
    `/api/users/${userId}/memberships`,
    body,
  );
}

/** Update roles / active for one company membership. */
export async function updateTenantUserMembership(
  userId: number,
  companyId: number,
  body: UpdateUserMembershipRequest,
): Promise<TenantUserDto> {
  return apiPut<UpdateUserMembershipRequest, TenantUserDto>(
    `/api/users/${userId}/memberships/${companyId}`,
    body,
  );
}

/**
 * Soft-deactivate membership.
 * Prefer company-scoped path; JWT-only path kept for Tenant Admin listing delete.
 */
export async function deactivateTenantUserMembership(
  userId: number,
  companyId?: number,
): Promise<void> {
  if (companyId != null && companyId > 0) {
    await apiDelete(`/api/users/${userId}/memberships/${companyId}`);
    return;
  }
  await apiDelete(`/api/users/${userId}/membership`);
}

/** Global Search Center — server TOP-N hits; pass AbortSignal to cancel in-flight traffic. */
export async function globalSearch(
  q: string,
  limit = 8,
  signal?: AbortSignal,
): Promise<GlobalSearchResponseDto> {
  const params = new URLSearchParams({
    q,
    limit: String(limit),
  });
  return apiGet<GlobalSearchResponseDto>(`/api/search?${params.toString()}`, {
    signal,
  });
}
