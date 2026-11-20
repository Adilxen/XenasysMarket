export type NavItem = {
  href: string;
  label: string;
  icon: NavIconName;
};

export type NavSection = {
  title?: string;
  items: NavItem[];
};

export type NavIconName =
  | "home"
  | "categories"
  | "subCategories"
  | "brands"
  | "products"
  | "stock"
  | "sales"
  | "vendors"
  | "coa"
  | "subsidiary"
  | "users"
  | "roles"
  | "company"
  | "settings"
  | "actions"
  | "formMap"
  | "audit";

/**
 * ==========================================================================================
 * WHAT: Main screen sidebar IA — operational modules + full SETTINGS module routes.
 * WHY: Settings is a real module (/settings/*) with DB+API+UI — not nav-only chrome.
 * EXAMPLE: SETTINGS → Company → /settings/company
 * ==========================================================================================
 */
export const MAIN_NAV: NavSection[] = [
  {
    items: [{ href: "/dashboard", label: "Dashboard", icon: "home" }],
  },
  {
    title: "CATALOG",
    items: [
      { href: "/categories", label: "Categories", icon: "categories" },
      { href: "/sub-categories", label: "Sub Categories", icon: "subCategories" },
      { href: "/brands", label: "Brands", icon: "brands" },
      { href: "/products", label: "Products / SKU", icon: "products" },
    ],
  },
  {
    title: "INVENTORY",
    items: [
      { href: "/stock", label: "Stock Management", icon: "stock" },
      { href: "/sales", label: "Sales Orders", icon: "sales" },
    ],
  },
  {
    title: "VENDOR",
    items: [{ href: "/vendors", label: "Vendors", icon: "vendors" }],
  },
  {
    title: "FINANCIALS",
    items: [
      { href: "/finance/coa", label: "COA Tree", icon: "coa" },
      { href: "/chart-of-accounts", label: "Chart of Accounts", icon: "coa" },
      { href: "/control-accounts", label: "Control Accounts", icon: "coa" },
      {
        href: "/subsidiary-accounts",
        label: "Subsidiary Accounts",
        icon: "subsidiary",
      },
    ],
  },
  {
    title: "SETTINGS",
    items: [
      { href: "/settings/company", label: "Company", icon: "company" },
      { href: "/settings/roles", label: "Roles", icon: "roles" },
      { href: "/settings/actions", label: "Actions", icon: "actions" },
      { href: "/settings/forms", label: "Forms", icon: "formMap" },
      { href: "/settings/users", label: "Users", icon: "users" },
      { href: "/settings/permission-audit", label: "Permission Audit", icon: "audit" },
      { href: "/settings/system", label: "System Settings", icon: "settings" },
    ],
  },
];

/** Exact-match for Settings leaves; nested active only for operational modules. */
export function isNavItemActive(pathname: string, href: string): boolean {
  if (href === "/dashboard") return pathname === "/dashboard";
  if (pathname === href) return true;
  if (href.startsWith("/settings/")) return false;
  return pathname.startsWith(`${href}/`);
}
