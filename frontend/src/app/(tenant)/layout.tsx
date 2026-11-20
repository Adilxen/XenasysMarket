import { TenantShell } from "@/components/layout/tenant-shell";

/**
 * ==========================================================================================
 * WHAT: Tenant route-group layout wrapping authenticated pages in Main screen chrome.
 * WHY: Server layout hosts client TenantShell (sidebar/header) without blocking RSC pages.
 * EXAMPLE: /dashboard, /products, /vendors all render inside AppSidebar + AppHeader.
 * ==========================================================================================
 */
export default function TenantLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <TenantShell>{children}</TenantShell>;
}
