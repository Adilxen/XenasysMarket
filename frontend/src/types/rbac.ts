import type { EntityId } from "@/types/api";

export interface RoleDto {
  id: EntityId;
  companyId: EntityId | null;
  /** Owning company name; empty for platform SUPER_ADMIN. */
  companyName?: string | null;
  roleCode: string;
  name: string;
  description?: string | null;
  /** Post-login landing path (e.g. /dashboard). */
  defaultRoute?: string | null;
  isActive: boolean;
  isSystem: boolean;
  permissionCodes?: string[];
}

export interface ActionDto {
  id: EntityId;
  name: string;
  isActive: boolean;
}

export interface CreateActionRequest {
  name: string;
}

export interface UpdateActionRequest {
  name: string;
  isActive: boolean;
}

export interface FormDto {
  id: EntityId;
  name: string;
  path: string;
  isActive: boolean;
}

export interface CreateFormRequest {
  name: string;
  path: string;
}

export interface UpdateFormRequest {
  name: string;
  path: string;
  isActive: boolean;
}

export interface PermissionOptionDto {
  code: string;
  module: string;
  action: string;
  description: string;
}

export interface PermissionDto {
  id: EntityId;
  code: string;
  module: string;
  action: string;
  description: string;
  isSystem: boolean;
}

export interface PermissionModuleGroupDto {
  module: string;
  permissions: PermissionDto[];
}

export interface PermissionAuditCellDto {
  permissionCode: string;
  granted: boolean;
}

export interface PermissionAuditRoleColumnDto {
  roleId: EntityId;
  roleCode: string;
  roleName: string;
  isSystem: boolean;
  cells: PermissionAuditCellDto[];
}

export interface PermissionAuditMatrixDto {
  permissions: PermissionDto[];
  roles: PermissionAuditRoleColumnDto[];
}

export interface CurrentUserPermissionsDto {
  role: string;
  companyId: EntityId | null;
  forms?: FormWithActionsDto[];
  permissionCodes: string[];
}

export interface FormActionRefDto {
  id: EntityId;
  name: string;
}

export interface FormWithActionsDto {
  id: EntityId;
  path: string;
  name: string;
  actions: FormActionRefDto[];
}

export interface FormActionAssignmentDto {
  actionId: EntityId;
  actionName: string;
  isAssigned: boolean;
}

export interface SyncFormActionsRequest {
  actionIds: number[];
}

export interface RoleFormActionItemDto {
  formId: number;
  actionIds: number[];
}

export interface SyncRoleFormActionsRequest {
  items: RoleFormActionItemDto[];
}

export interface RoleFormActionsDto {
  roleId: EntityId;
  forms: FormWithActionsDto[];
}

/** newSalon-style Permission Audit — role chip on an action. */
export interface FormActionAuditRoleRefDto {
  roleId: EntityId;
  roleName: string;
  roleCode?: string;
}

export interface FormActionAuditActionDto {
  actionId: EntityId;
  actionName: string;
  roles: FormActionAuditRoleRefDto[];
}

export interface FormActionAuditFormDto {
  formId: EntityId;
  formPath: string;
  formName: string;
  totalActionCount: number;
  assignedActionCount: number;
  actions: FormActionAuditActionDto[];
}

export interface FormActionAuditMatrixDto {
  forms: FormActionAuditFormDto[];
}

export interface CurrentUserProfileDto {
  userId: EntityId;
  userName: string;
  email?: string | null;
  role: string;
  roleName: string;
  companyId: EntityId;
  companyName: string;
  defaultRoute?: string | null;
}

export interface UpdateCurrentUserProfileRequest {
  userName: string;
  /** Omit / blank = keep existing password. */
  password?: string | null;
}

export interface CreateRoleRequest {
  name: string;
  description?: string | null;
  defaultRoute?: string | null;
  /** Super Admin: target company (wire key company_id). */
  company_id?: number | null;
  permissionCodes?: string[];
}

/** @deprecated Prefer CreateRoleRequest — code is server-allocated. */
export interface CreateCustomRoleRequest {
  code?: string;
  name: string;
  permissionCodes?: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description?: string | null;
  defaultRoute?: string | null;
  isActive: boolean;
}

export interface UpdateRolePermissionsRequest {
  permissionCodes: string[];
}

export interface TenantUserDto {
  id: EntityId;
  userName: string;
  email: string | null;
  isActive: boolean;
  membershipId: EntityId;
  /** Primary / JWT display role (first assigned). */
  roleId: EntityId;
  roleCode: string;
  roleName: string;
  /** All company roles assigned to this user (T1.38). */
  roles?: UserRoleRefDto[];
  membershipIsActive: boolean;
  companyId: EntityId;
  companyName: string;
  tenantCode?: string | null;
  /** All company memberships for this user. */
  companies?: UserCompanyRefDto[];
}

export interface UserRoleRefDto {
  roleId: EntityId;
  roleCode: string;
  roleName: string;
}

export interface UserCompanyRefDto {
  companyId: EntityId;
  companyName: string;
  tenantCode?: string | null;
  membershipIsActive: boolean;
  /** Roles for this company membership (T1.56). */
  roles?: UserRoleRefDto[];
}

export interface CreateTenantUserRequest {
  userName: string;
  password: string;
  email?: string | null;
  roleIds: EntityId[];
  /** Super Admin: target company (wire key company_id). */
  company_id?: number | null;
}

export interface UpdateTenantUserRequest {
  email?: string | null;
  roleIds?: EntityId[];
  membershipIsActive?: boolean;
  /** Super Admin membership target (wire key company_id). Omit = profile-only. */
  company_id?: number | null;
  /** Omit / blank = keep existing password. */
  password?: string | null;
}

export interface AddUserMembershipRequest {
  company_id: number;
  roleIds: EntityId[];
}

export interface UpdateUserMembershipRequest {
  roleIds: EntityId[];
  membershipIsActive: boolean;
}
