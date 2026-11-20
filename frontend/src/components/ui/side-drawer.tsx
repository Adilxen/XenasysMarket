"use client";

import { useEffect, type ReactNode } from "react";
import {
  btnCancelClass,
  btnPrimaryClass,
  drawerFooterClass,
  drawerOverlayClass,
  drawerPanelClass,
} from "@/lib/forms/field-styles";

/**
 * Right slide-over drawer (Add Category pattern) with dim overlay + footer actions.
 */
export function SideDrawer({
  open,
  title,
  onClose,
  children,
  primaryLabel = "Save",
  onPrimary,
  primaryDisabled,
  primaryLoading,
  cancelLabel = "Cancel",
  wide,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  primaryLabel?: string;
  onPrimary?: () => void;
  primaryDisabled?: boolean;
  primaryLoading?: boolean;
  cancelLabel?: string;
  wide?: boolean;
}) {
  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <button
        type="button"
        aria-label="Close drawer"
        className={drawerOverlayClass()}
        onClick={onClose}
      />
      <aside
        className={[
          drawerPanelClass(),
          wide ? "max-w-lg" : "max-w-md",
          "relative z-10 animate-in slide-in-from-right duration-200",
        ].join(" ")}
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <header className="flex items-center justify-between border-b border-[#E8ECF2] px-5 py-4">
          <h2 className="text-lg font-bold tracking-tight text-[#1A2238]">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 text-[#9CA3AF] transition hover:bg-[#F3F5F9] hover:text-[#4B5563]"
            aria-label="Close"
          >
            <CloseIcon />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5">{children}</div>

        <footer className={drawerFooterClass()}>
          <button type="button" className={btnCancelClass()} onClick={onClose}>
            {cancelLabel}
          </button>
          {onPrimary ? (
            <button
              type="button"
              className={btnPrimaryClass()}
              disabled={primaryDisabled || primaryLoading}
              onClick={onPrimary}
            >
              {primaryLoading ? "Saving…" : primaryLabel}
            </button>
          ) : null}
        </footer>
      </aside>
    </div>
  );
}

function CloseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M6 6l12 12M18 6L6 18"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  );
}
