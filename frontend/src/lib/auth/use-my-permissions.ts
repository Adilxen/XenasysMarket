"use client";

/**
 * ==========================================================================================
 * WHAT: Current-user permissions — Form×Action (hasForm/hasAction) + legacy claim codes.
 * WHY: T1.34 newSalon-style nav/route gates; ClaimGate still uses derived permissionCodes.
 * EXAMPLE: hasForm("/settings/users"); hasAction("/products", "Create"); can("users.read")
 * ==========================================================================================
 */

import { useCallback, useEffect, useMemo, useState } from "react";
import { getMyPermissions } from "@/lib/api/endpoints";
import { hasClaim } from "@/lib/auth/permission-codes";
import { sessionStore } from "@/lib/auth/session";
import type { FormWithActionsDto } from "@/types/rbac";

type PermissionsState = {
  codes: string[];
  forms: FormWithActionsDto[];
  role: string | null;
  ready: boolean;
  error: string | null;
};

let cache: {
  codes: string[];
  forms: FormWithActionsDto[];
  role: string | null;
} | null = null;

export function normalizeFormPath(path: string | null | undefined): string {
  if (!path) return "";
  let p = path.trim().toLowerCase().replace(/\\/g, "/");
  if (!p.startsWith("/")) p = `/${p}`;
  if (p.length > 1 && p.endsWith("/")) p = p.slice(0, -1);
  return p;
}

function actionNamesMatch(a: string, b: string): boolean {
  const na = a.trim().toLowerCase();
  const nb = b.trim().toLowerCase();
  if (na === nb) return true;
  if (
    (na === "read" || na === "view") &&
    (nb === "read" || nb === "view")
  ) {
    return true;
  }
  if (
    (na === "update" || na === "edit") &&
    (nb === "update" || nb === "edit")
  ) {
    return true;
  }
  return false;
}

export function invalidateMyPermissionsCache() {
  cache = null;
}

export function useMyPermissions() {
  const [state, setState] = useState<PermissionsState>(() => ({
    codes: cache?.codes ?? [],
    forms: cache?.forms ?? [],
    role: cache?.role ?? sessionStore.getRole(),
    ready: Boolean(cache),
    error: null,
  }));

  useEffect(() => {
    if (cache) {
      setState({
        codes: cache.codes,
        forms: cache.forms,
        role: cache.role,
        ready: true,
        error: null,
      });
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        const me = await getMyPermissions();
        if (cancelled) return;
        cache = {
          codes: me.permissionCodes ?? [],
          forms: me.forms ?? [],
          role: me.role,
        };
        setState({
          codes: cache.codes,
          forms: cache.forms,
          role: cache.role,
          ready: true,
          error: null,
        });
      } catch {
        if (cancelled) return;
        const role = sessionStore.getRole();
        // Fail closed for UI — never invent platform Forms/Actions grants for Tenant Admin.
        const roleUpper = role?.toUpperCase() ?? "";
        const fallback =
          roleUpper === "SUPER_ADMIN"
            ? [
                "users.read",
                "users.manage",
                "roles.read",
                "roles.create",
                "roles.update",
                "roles.delete",
                "company.read",
                "company.create",
                "company.update",
                "company.delete",
                "categories.read",
                "brands.read",
                "products.read",
                "stock.read",
                "vendors.read",
                "coa.masters.read",
                "coa.controls.read",
                "coa.subsidiaries.read",
                "dashboard.read",
                "platform.read",
                "forms.read",
                "forms.manage",
                "actions.read",
                "actions.manage",
              ]
            : roleUpper === "TENANT_ADMIN"
              ? [
                  "users.read",
                  "users.manage",
                  "roles.read",
                  "roles.create",
                  "roles.update",
                  "roles.delete",
                  "company.read",
                  "company.update",
                  "categories.read",
                  "brands.read",
                  "products.read",
                  "stock.read",
                  "vendors.read",
                  "coa.masters.read",
                  "coa.controls.read",
                  "coa.subsidiaries.read",
                  "dashboard.read",
                ]
              : [];
        cache = { codes: fallback, forms: [], role };
        setState({
          codes: fallback,
          forms: [],
          role,
          ready: true,
          error:
            "Unable to load permission claims. Some modules may be hidden until refresh.",
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const formByPath = useMemo(() => {
    const map = new Map<string, FormWithActionsDto>();
    for (const f of state.forms) {
      map.set(normalizeFormPath(f.path), f);
    }
    return map;
  }, [state.forms]);

  const hasForm = useCallback(
    (path: string) => {
      const role = (state.role ?? "").toUpperCase();
      if (role === "SUPER_ADMIN") return true;
      const key = normalizeFormPath(path);
      if (key === "/dashboard") return true;
      if (formByPath.has(key)) return true;
      for (const granted of formByPath.keys()) {
        if (granted !== "/" && key.startsWith(`${granted}/`)) return true;
      }
      return false;
    },
    [formByPath, state.role],
  );

  const hasAction = useCallback(
    (path: string, actionName: string) => {
      const role = (state.role ?? "").toUpperCase();
      if (role === "SUPER_ADMIN") return true;
      const form = formByPath.get(normalizeFormPath(path));
      if (!form?.actions?.length) return false;
      return form.actions.some((a) => actionNamesMatch(a.name, actionName));
    },
    [formByPath, state.role],
  );

  const can = useCallback(
    (code: string) => {
      const role = (state.role ?? "").toUpperCase();
      if (role === "SUPER_ADMIN") return true;
      return hasClaim(state.codes, code);
    },
    [state.codes, state.role],
  );

  const canAny = useCallback(
    (required: readonly string[]) => {
      const role = (state.role ?? "").toUpperCase();
      if (role === "SUPER_ADMIN") return true;
      return required.some((c) => hasClaim(state.codes, c));
    },
    [state.codes, state.role],
  );

  return {
    ...state,
    can,
    canAny,
    hasForm,
    hasAction,
    formsByPath: formByPath,
  };
}

/** Convenience flags for a single form path (Create/Read/Update/Delete/View). */
export function useFormActions(path: string) {
  const { hasAction, ready } = useMyPermissions();
  return {
    ready,
    canCreate: hasAction(path, "Create"),
    canRead: hasAction(path, "Read") || hasAction(path, "View"),
    canUpdate: hasAction(path, "Update"),
    canDelete: hasAction(path, "Delete"),
    canView: hasAction(path, "View") || hasAction(path, "Read"),
  };
}
