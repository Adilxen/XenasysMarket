"use client";

/**
 * ==========================================================================================
 * WHAT: Settings page chrome — header only (no redundant tab strip; sidebar owns nav).
 * WHY: Category Listing pattern; Settings children live in sidebar dropdown.
 * EXAMPLE: <SettingsPageChrome title="Company" fill>…listing card…
 * ==========================================================================================
 */

import { type ReactNode } from "react";
import { PageHeader } from "@/components/ui/page-header";
import {
  canAccessTab,
  type SettingsTabId,
} from "@/lib/auth/permission-codes";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";
import { pageCanvasClass, pageCanvasFillClass } from "@/lib/forms/field-styles";

export type PermissionTabId = SettingsTabId;

export function SettingsPageChrome({
  active,
  title,
  subtitle,
  breadcrumb = "Settings",
  actions,
  fill = false,
  children,
}: {
  active: SettingsTabId;
  title: string;
  subtitle?: string;
  /** Pass `null` to hide breadcrumb (e.g. Company page — title only). */
  breadcrumb?: string | null;
  actions?: ReactNode;
  /** Fill tenant main height — listing pages (heading + card, no page scroll). */
  fill?: boolean;
  children: ReactNode;
}) {
  const { codes, ready, hasForm, forms } = useMyPermissions();
  const pathForTab =
    active === "permission-audit"
      ? "/settings/permission-audit"
      : `/settings/${active}`;
  const canSeeActive =
    forms.length > 0 ? hasForm(pathForTab) : canAccessTab(codes, active);

  const canvas = fill
    ? pageCanvasFillClass()
    : pageCanvasClass("h-full overflow-y-auto");

  if (!ready) {
    return (
      <section className={canvas}>
        <div className="rounded-xl border border-[#E8ECF2] bg-white px-4 py-8 text-center text-sm text-[#8090A8]">
          Resolving access claims…
        </div>
      </section>
    );
  }

  // FormRouteGate owns Access Restricted popup for URL / deep-link denials.
  if (!canSeeActive) {
    return null;
  }

  return (
    <section className={canvas}>
      <div className="shrink-0">
        <PageHeader
          breadcrumb={breadcrumb ?? undefined}
          title={title}
          subtitle={subtitle}
          actions={actions}
        />
      </div>
      <div
        className={
          fill
            ? "flex min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden"
            : "w-full min-w-0"
        }
      >
        {children}
      </div>
    </section>
  );
}

/** @deprecated Prefer SettingsPageChrome */
export const SettingsModuleShell = SettingsPageChrome;
export const PermissionManagementShell = SettingsPageChrome;
