/** Surrogate PK / FK — T0.4 INT IDENTITY (was Guid). */
export type EntityId = number;

/** @deprecated Prefer EntityId — kept as alias during migration. */
export type Guid = EntityId;

/**
 * ==========================================================================================
 * WHAT: Login wire DTO — userName, password, and company_id INT (nullable until selected).
 * WHY: Mirrors LoginRequestDto; RHF+Zod maps company name→company_id before POST.
 * EXAMPLE: { userName: "subhan", password: "••••••••", company_id: 12 }.
 * ==========================================================================================
 */
export interface LoginRequest {
  userName: string;
  password: string;
  company_id: EntityId | null;
}

/**
 * ==========================================================================================
 * WHAT: Successful login JWT payload with tenant/company context after auth.
 * WHY: Session store needs token + tenantId; companyName is display-only (id already chosen).
 * EXAMPLE: { accessToken, userName, userId: 1, tenantId: 12, companyName: "Acme", role: "Admin" }.
 * ==========================================================================================
 */
export interface LoginResponse {
  token: string;
  accessToken: string;
  userName: string;
  userId: EntityId;
  tenantId: EntityId;
  companyName: string;
  role: string;
  /** Role.DefaultRoute — post-login landing (fallback /dashboard). */
  defaultRoute?: string | null;
}

/** Multi-company challenge after BCrypt verify (no JWT yet). */
export interface LoginCompanyOption {
  tenantId: EntityId;
  tenantCode: string;
  companyName: string;
  role: string;
}

/**
 * ==========================================================================================
 * WHAT: Multi-company challenge when credentials verify but company_id was missing/ambiguous.
 * WHY: LoginForm must re-prompt company name→company_id INT (RHF+Zod onBlur / #DC2626).
 * EXAMPLE: { requiresCompanySelection: true, userName, userId, companies: [{ tenantId: 12, ... }] }.
 * ==========================================================================================
 */
export interface LoginCompanyChallenge {
  requiresCompanySelection: true;
  userName: string;
  userId: EntityId;
  companies: LoginCompanyOption[];
}

export type LoginResult = LoginResponse | LoginCompanyChallenge;

export function isLoginChallenge(
  result: LoginResult,
): result is LoginCompanyChallenge {
  return (
    "requiresCompanySelection" in result && result.requiresCompanySelection === true
  );
}

/** Mirrors Xenasys.Core.Contracts.Products.CreateProductRequest */
export interface CreateProductRequest {
  companyId?: EntityId | null;
  productName: string;
  productDescription?: string | null;
  packaging: string;
  categoryId: EntityId;
  subCategoryId: EntityId;
  brandId: EntityId;
  unitId: EntityId;
  invoicePrice: number;
  retailPrice: number;
  minimumStockLevel: number;
  barcode?: string | null;
  imagePath?: string | null;
}

/** Mirrors Xenasys.Core.Contracts.Products.UpdateProductRequest */
export interface UpdateProductRequest {
  productName: string;
  productDescription?: string | null;
  packaging: string;
  categoryId: EntityId;
  subCategoryId: EntityId;
  brandId: EntityId;
  unitId: EntityId;
  invoicePrice: number;
  retailPrice: number;
  minimumStockLevel: number;
  barcode?: string | null;
  imagePath?: string | null;
  isActive?: boolean | null;
}

/** Mirrors Xenasys.Core.Contracts.Products.ProductDto */
export interface ProductDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  categoryId: EntityId;
  categoryName?: string | null;
  subCategoryId: EntityId;
  subCategoryName?: string | null;
  brandId: EntityId;
  brandName?: string | null;
  unitId: EntityId;
  unitCode?: string | null;
  productName: string;
  productDescription?: string | null;
  packaging: string;
  /** Denormalized unit code snapshot from Unit.Code on write. */
  unitOfMeasurement: string;
  /** CHAR(16) composite: TenantCode + UserNn + seq:D8 — server-allocated. */
  productCode: string;
  barcode?: string | null;
  minimumStockLevel: number;
  quantityOnHand: number;
  invoicePrice: number;
  retailPrice: number;
  imagePath?: string;
  isActive: boolean;
  subsidiaryAccountId?: EntityId | null;
}

/** Mirrors Xenasys.Core.Contracts.Tenants.TenantDto */
export interface TenantDto {
  id: EntityId;
  name: string;
  tenantCode: string;
  isActive: boolean;
  contactEmail: string | null;
  contactPhone: string | null;
  address?: string | null;
}

export interface CreateTenantRequest {
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
}

export interface UpdateTenantRequest {
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
  isActive?: boolean | null;
}

export interface UpdateTenantProfileRequest {
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
}

/** Mirrors Xenasys.Core.Contracts.Coa.MasterAccountDto */
export interface MasterAccountDto {
  id: EntityId;
  code: string;
  title: string;
  prefix: string;
  isActive: boolean;
}

export interface CreateMasterAccountRequest {
  code: string;
  title: string;
  prefix: string;
}

export interface UpdateMasterAccountRequest {
  code: string;
  title: string;
  prefix: string;
  isActive?: boolean | null;
}

/** Mirrors Xenasys.Core.Contracts.Coa.ControlAccountDto */
export interface ControlAccountDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  companyCode?: string | null;
  masterAccountId: EntityId;
  masterAccountCode?: string | null;
  masterAccountTitle?: string | null;
  code: string;
  title: string;
  controlKind: string;
  isActive: boolean;
}

export interface CreateControlAccountRequest {
  companyId?: EntityId | null;
  masterAccountId: EntityId;
  title: string;
  controlKind: string;
}

export interface UpdateControlAccountRequest {
  masterAccountId: EntityId;
  title: string;
  controlKind: string;
  isActive?: boolean | null;
}

/** Mirrors Xenasys.Core.Contracts.Accounts.SubsidiaryAccountDto */
export interface SubsidiaryAccountDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  companyCode?: string | null;
  controlAccountId: EntityId;
  controlAccountCode?: string | null;
  controlAccountTitle?: string | null;
  subsidiaryAccountCode: string;
  subsidiaryAccountTitle: string;
  accountNo: string;
  isActive: boolean;
  isSystemGenerated: boolean;
  /** Parent control kind — INVENTORY | ACCOUNTS_PAYABLE | OTHER */
  controlKind?: string | null;
  productCode?: string | null;
}

export interface CreateSubsidiaryAccountRequest {
  controlAccountId: EntityId;
  subsidiaryAccountTitle: string;
}

export interface UpdateSubsidiaryAccountRequest {
  controlAccountId: EntityId;
  subsidiaryAccountTitle: string;
  isActive?: boolean | null;
}

/** Mirrors Xenasys.Core.Abstractions.Pagination.KeysetPageRequest */
export interface KeysetPageRequest {
  afterId?: EntityId | null;
  limit?: number;
  includeInactive?: boolean;
  shape?: "list" | "detail";
  /** Super Admin: scope catalog/product dropdowns to JWT / selected company. */
  companyId?: EntityId | null;
  /** M5: stock list — only rows where qty <= minimum. */
  lowStockOnly?: boolean;
  /** T1.52 column Contains filters (e.g. productName, productCode). */
  filters?: Record<string, string>;
}

/** Mirrors Xenasys.Core.Abstractions.Pagination.KeysetPageResult<T> */
export interface KeysetPageResult<T> {
  items: T[];
  nextCursor: EntityId | null;
  hasMore: boolean;
  /** Filtered row count (excludes keyset cursor). */
  totalCount?: number | null;
}

/** Mirrors Xenasys.Core.Contracts.Vendors.VendorDto */
export interface VendorDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  vendorName: string;
  ntnNo?: string | null;
  address: string;
  vendorPhone: string;
  email: string | null;
  representativeName: string;
  representativeNo: string;
  isActive: boolean;
  subsidiaryAccountId: EntityId | null;
}

export interface CreateVendorRequest {
  companyId?: EntityId | null;
  vendorName: string;
  ntnNo?: string | null;
  address?: string | null;
  vendorPhone: string;
  email?: string | null;
  representativeName?: string | null;
  representativeNo?: string | null;
}

export interface UpdateVendorRequest {
  vendorName: string;
  ntnNo?: string | null;
  address?: string | null;
  vendorPhone: string;
  email?: string | null;
  representativeName?: string | null;
  representativeNo?: string | null;
  isActive?: boolean | null;
}

export interface CreateBrandRequest {
  companyId?: EntityId | null;
  name: string;
  description?: string | null;
}

export interface UpdateBrandRequest {
  name: string;
  description?: string | null;
  isActive?: boolean | null;
}

export interface FieldErrors {
  errors: Record<string, string[]>;
}

/** Production Dashboard (M7) */
export interface DashboardTotalsDto {
  companyCount: number;
  activeProductCount: number;
  totalQuantityOnHand: number;
  lowStockProductCount: number;
  activeVendorCount: number;
  pendingLowStockAlerts: number;
}

export interface DashboardInventoryRowDto {
  companyId: EntityId;
  companyName: string;
  tenantCode: string;
  productCount: number;
  activeProductCount: number;
  totalQuantityOnHand: number;
  lowStockProductCount: number;
  activeVendorCount: number;
}

export interface DashboardLowStockItemDto {
  productId: EntityId;
  companyId: EntityId;
  companyName: string;
  productName: string;
  productCode: string;
  quantityOnHand: number;
  minimumStockLevel: number;
}

export interface DashboardCompanyOptionDto {
  companyId: EntityId;
  companyName: string;
  tenantCode: string;
}

export interface DashboardDto {
  isSuperAdmin: boolean;
  scopedCompanyId?: EntityId | null;
  scopedCompanyName?: string | null;
  totals: DashboardTotalsDto;
  inventory: DashboardInventoryRowDto[];
  inventoryNextCursor?: EntityId | null;
  inventoryHasMore: boolean;
  inventoryTotalCount: number;
  lowStockItems: DashboardLowStockItemDto[];
  companyOptions: DashboardCompanyOptionDto[];
  salesModuleLive: boolean;
  salesMessage?: string | null;
}

/** @deprecated Use DashboardInventoryRowDto */
export type SuperAdminInventorySummaryDto = DashboardInventoryRowDto;
/** @deprecated Use DashboardDto */
export type SuperAdminDashboardDto = DashboardDto;

/** M5 Stock Management */
export interface StockItemDto {
  productId: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  productName: string;
  productCode: string;
  unitOfMeasurement: string;
  quantityOnHand: number;
  minimumStockLevel: number;
  isLowStock: boolean;
  isActive: boolean;
}

export interface UpdateStockRequest {
  quantityOnHand?: number | null;
  quantityDelta?: number | null;
  reason?: string | null;
  minimumStockLevel?: number | null;
}

export interface StockMovementDto {
  id: EntityId;
  productId: EntityId;
  quantityDelta: number;
  quantityAfter: number;
  reason?: string | null;
  occurredAt: string;
  insertUserId: number;
}

export interface StockAlertDto {
  outboxId?: EntityId | null;
  productId: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  productName: string;
  productCode: string;
  quantityOnHand: number;
  minimumStockLevel: number;
  status: string;
  insertDate: string;
}
