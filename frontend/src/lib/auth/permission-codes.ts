/**
 * ==========================================================================================
 * WHAT: Frontend permission code constants aligned to PermissionCatalog.
 * WHY: Claim-gated DOM — hide unauthorized Settings / operational surfaces entirely.
 * EXAMPLE: navClaimsForHref("/settings/users") → ["users.read"]; empty only for Dashboard.
 * ==========================================================================================
 */

export const PERMISSION = {
  UsersRead: "users.read",
  UsersManage: "users.manage",
  RolesRead: "roles.read",
  RolesCreate: "roles.create",
  RolesUpdate: "roles.update",
  RolesDelete: "roles.delete",
  /** @deprecated Prefer RolesUpdate */
  RolesManage: "roles.manage",
  CompanyRead: "company.read",
  CompanyCreate: "company.create",
  CompanyUpdate: "company.update",
  CompanyDelete: "company.delete",
  CategoriesRead: "categories.read",
  BrandsRead: "brands.read",
  ProductsRead: "products.read",
  StockRead: "stock.read",
  SalesRead: "sales.read",
  PlatformRead: "platform.read",
  DashboardRead: "dashboard.read",
  VendorsRead: "vendors.read",
  CoaMastersRead: "coa.masters.read",
  CoaMastersCreate: "coa.masters.create",
  CoaMastersUpdate: "coa.masters.update",
  CoaMastersDelete: "coa.masters.delete",
  CoaControlsRead: "coa.controls.read",
  CoaControlsCreate: "coa.controls.create",
  CoaControlsUpdate: "coa.controls.update",
  CoaControlsDelete: "coa.controls.delete",
  CoaSubsidiariesRead: "coa.subsidiaries.read",
  FormsRead: "forms.read",
  FormsManage: "forms.manage",
  ActionsRead: "actions.read",
  ActionsManage: "actions.manage",
} as const;

export type SettingsTabId =
  | "company"
  | "users"
  | "roles"
  | "actions"
  | "forms"
  | "permission-audit";

export type UserManagementTabId = SettingsTabId;

export const TAB_REQUIRED_CLAIMS: Record<SettingsTabId, readonly string[]> = {
  company: [PERMISSION.CompanyRead],
  users: [PERMISSION.UsersRead],
  roles: [PERMISSION.RolesRead],
  actions: [PERMISSION.ActionsRead],
  forms: [PERMISSION.FormsRead],
  "permission-audit": [PERMISSION.RolesRead],
};

export function hasAnyClaim(
  granted: readonly string[] | null | undefined,
  required: readonly string[],
): boolean {
  if (!granted?.length) return false;
  const set = new Set(granted.map((c) => c.toLowerCase()));
  return required.some((c) => set.has(c.toLowerCase()));
}

export function hasClaim(
  granted: readonly string[] | null | undefined,
  code: string,
): boolean {
  return hasAnyClaim(granted, [code]);
}

export function canAccessTab(
  granted: readonly string[] | null | undefined,
  tab: SettingsTabId,
): boolean {
  const required = TAB_REQUIRED_CLAIMS[tab];
  if (required.length === 0) return false;
  return hasAnyClaim(granted, required);
}

/**
 * Sidebar / command-palette href → required claim(s).
 * Empty array = authenticated shell only (Dashboard). Every other route must declare a claim.
 */
export function navClaimsForHref(href: string): readonly string[] {
  if (href === "/dashboard") {
    return [];
  }

  switch (href) {
    case "/settings/company":
    case "/company-profile":
      return [PERMISSION.CompanyRead];
    case "/settings/roles":
    case "/settings/permission-audit":
    case "/permission-management/roles":
    case "/permission-management/permission-audit":
      return [PERMISSION.RolesRead];
    case "/settings/actions":
    case "/permission-management/actions":
      return [PERMISSION.ActionsRead];
    case "/settings/forms":
    case "/permission-management/forms":
      return [PERMISSION.FormsRead];
    case "/settings/users":
    case "/permission-management/users":
      return [PERMISSION.UsersRead];
    case "/settings/system":
      return [PERMISSION.PlatformRead];
    default:
      break;
  }

  if (href.startsWith("/categories") || href.startsWith("/sub-categories")) {
    return [PERMISSION.CategoriesRead];
  }
  if (href.startsWith("/brands")) {
    return [PERMISSION.BrandsRead];
  }
  if (href.startsWith("/products")) {
    return [PERMISSION.ProductsRead];
  }
  if (href.startsWith("/stock")) return [PERMISSION.StockRead];
  if (href.startsWith("/sales")) return [PERMISSION.SalesRead];
  if (href.startsWith("/vendors")) return [PERMISSION.VendorsRead];
  if (href.startsWith("/subsidiary-accounts")) {
    return [PERMISSION.CoaSubsidiariesRead];
  }
  if (href.startsWith("/control-accounts")) {
    return [PERMISSION.CoaControlsRead];
  }
  if (href.startsWith("/chart-of-accounts") || href.startsWith("/finance/coa")) {
    return [PERMISSION.CoaMastersRead];
  }

  // Unknown routes: deny until an explicit claim is mapped.
  return ["__deny__"];
}

/** True when href may appear for the granted claim set. */
export function canAccessHref(
  granted: readonly string[] | null | undefined,
  href: string,
): boolean {
  const required = navClaimsForHref(href);
  if (!required.length) return href === "/dashboard";
  return hasAnyClaim(granted, required);
}
