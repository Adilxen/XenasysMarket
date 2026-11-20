"use client";

import type { ReactNode } from "react";
import {
  breadcrumbClass,
  pageSubtitleClass,
  pageTitleClass,
} from "@/lib/forms/field-styles";

/**
 * Category Listing–style page header: breadcrumb + title + subtitle + actions.
 * Fluid across breakpoints — actions wrap under title on narrow canvases.
 */
export function PageHeader({
  breadcrumb,
  title,
  subtitle,
  actions,
}: {
  breadcrumb?: string;
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="flex w-full min-w-0 flex-col gap-4 sm:flex-row sm:items-start sm:justify-between lg:flex-nowrap">
      <div className="min-w-0 flex-1">
        {breadcrumb ? <p className={breadcrumbClass()}>{breadcrumb}</p> : null}
        <h1 className={`${pageTitleClass()} ${breadcrumb ? "mt-1" : ""}`}>{title}</h1>
        {subtitle ? <p className={pageSubtitleClass()}>{subtitle}</p> : null}
      </div>
      {actions ? (
        <div className="flex w-full shrink-0 flex-wrap items-center gap-2 sm:w-auto">
          {actions}
        </div>
      ) : null}
    </div>
  );
}
