"use client";

/**
 * ==========================================================================================
 * WHAT: Route guard — unauthorized Form path shows Access Restricted popup, then Dashboard.
 * WHY: URL bar / deep links must not silently leak or redirect without explicit restrict UX.
 * EXAMPLE: No grant for /settings/forms → popup → OK → /dashboard.
 * ==========================================================================================
 */

import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { AccessRestrictedDialog } from "@/components/auth/access-restricted-dialog";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";

const ALWAYS_ALLOWED = new Set(["/dashboard", "/login"]);

function pathLabel(path: string): string {
  const map: Record<string, string> = {
    "/settings/company": "Company",
    "/company-profile": "My Company",
    "/settings/roles": "Roles",
    "/settings/users": "Users",
    "/settings/forms": "Forms",
    "/settings/actions": "Actions",
    "/settings/permission-audit": "Permission Audit",
    "/settings/system": "System Settings",
    "/categories": "Categories",
    "/sub-categories": "Sub Categories",
    "/brands": "Brands",
    "/products": "Products",
    "/stock": "Stock Management",
    "/vendors": "Vendors",
    "/chart-of-accounts": "Chart of Accounts",
    "/control-accounts": "Control Accounts",
    "/subsidiary-accounts": "Subsidiary Accounts",
    "/finance/coa": "COA Tree",
  };
  return map[path] ?? path;
}

export function FormRouteGate({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { ready, hasForm } = useMyPermissions();
  const [deniedPath, setDeniedPath] = useState<string | null>(null);

  const path = useMemo(() => {
    return (pathname ?? "/").split("?")[0] || "/";
  }, [pathname]);

  const allowed = useMemo(() => {
    if (!ready) return true;
    if (ALWAYS_ALLOWED.has(path) || path === "/") return true;
    // Form×Action SSoT (SUPER_ADMIN bypass inside hasForm).
    return hasForm(path);
  }, [ready, path, hasForm]);

  useEffect(() => {
    if (!ready) return;
    if (allowed) {
      setDeniedPath(null);
      return;
    }
    setDeniedPath(path);
  }, [ready, allowed, path]);

  const acknowledge = useCallback(() => {
    setDeniedPath(null);
    router.replace("/dashboard");
  }, [router]);

  const blocked = ready && !allowed;

  return (
    <>
      {blocked ? null : children}
      <AccessRestrictedDialog
        open={deniedPath != null}
        screenName={deniedPath ? pathLabel(deniedPath) : undefined}
        onAcknowledge={acknowledge}
      />
    </>
  );
}
