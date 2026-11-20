"use client";

import type { ReactNode } from "react";
import { dataCardClass, listingToolbarClass } from "@/lib/forms/field-styles";

/**
 * ==========================================================================================
 * WHAT: White listing card — toolbar + table body (+ optional footer).
 * WHY: `fill` makes the card consume remaining viewport under the page heading.
 * EXAMPLE: <DataCard fill toolbar={…}><DataTable footerStickyToParent … /></DataCard>
 * ==========================================================================================
 */
export function DataCard({
  toolbar,
  footer,
  fill = false,
  children,
}: {
  toolbar?: ReactNode;
  footer?: ReactNode;
  /** Grow to fill parent height; body scrolls via DataTable footerStickyToParent. */
  fill?: boolean;
  children: ReactNode;
}) {
  return (
    <div
      className={[
        dataCardClass(),
        "w-full min-w-0",
        fill ? "flex min-h-0 flex-1 flex-col overflow-hidden" : "",
      ]
        .filter(Boolean)
        .join(" ")}
    >
      {toolbar ? (
        <div
          className={`${listingToolbarClass()} shrink-0 border-b border-[#E8ECF2] px-4 py-3`}
        >
          {toolbar}
        </div>
      ) : null}
      <div
        className={
          fill
            ? "flex min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden"
            : "w-full min-w-0"
        }
      >
        {children}
      </div>
      {footer ? (
        <div className="flex w-full min-w-0 shrink-0 flex-col gap-2 border-t border-[#E8ECF2] px-4 py-3 text-sm text-[#6B7280] sm:flex-row sm:items-center sm:justify-between">
          {footer}
        </div>
      ) : null}
    </div>
  );
}
