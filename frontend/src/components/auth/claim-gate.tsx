"use client";

/**
 * ==========================================================================================
 * WHAT: Claim / Form×Action gate — hide unauthorized UI (not disabled chrome).
 * WHY: Prefer hasAction(path, action) when Form×Action payload exists; else legacy codes.
 * EXAMPLE: <ClaimGate formPath="/settings/users" action="Create"><Add /></ClaimGate>
 * ==========================================================================================
 */

import type { ReactNode } from "react";
import { hasAnyClaim, hasClaim } from "@/lib/auth/permission-codes";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";

export function ClaimGate({
  claim,
  anyOf,
  formPath,
  action,
  children,
  fallback = null,
}: {
  /** Legacy PermissionCatalog code (e.g. users.manage). */
  claim?: string;
  anyOf?: readonly string[];
  /** Form route path for RoleFormActions check. */
  formPath?: string;
  /** Action name: Create | Read | Update | Delete | View. */
  action?: string;
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { codes, ready, hasAction, forms } = useMyPermissions();
  if (!ready) return null;

  if (formPath && action && forms.length > 0) {
    return hasAction(formPath, action) ? <>{children}</> : <>{fallback}</>;
  }

  const ok = claim
    ? hasClaim(codes, claim)
    : anyOf
      ? hasAnyClaim(codes, anyOf)
      : false;

  if (!ok) return <>{fallback}</>;
  return <>{children}</>;
}
