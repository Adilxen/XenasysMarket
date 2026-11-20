"use client";

/**
 * ==========================================================================================
 * WHAT: Centered confirmation dialog (delete / destructive actions).
 * WHY: Replace browser window.confirm with Category-pattern professional chrome.
 * EXAMPLE: <ConfirmDialog open={…} title="Delete company?" onConfirm={…} onCancel={…} />
 * ==========================================================================================
 */

import { useEffect, type ReactNode } from "react";
import {
  btnCancelClass,
  btnDangerClass,
  btnPrimaryClass,
  drawerOverlayClass,
} from "@/lib/forms/field-styles";

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  tone = "danger",
  loading = false,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  description?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: "danger" | "primary";
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape" && !loading) onCancel();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, loading, onCancel]);

  if (!open) return null;

  const confirmClass = tone === "danger" ? btnDangerClass() : btnPrimaryClass();

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center p-4">
      <button
        type="button"
        aria-label="Dismiss confirmation"
        className={drawerOverlayClass()}
        disabled={loading}
        onClick={() => {
          if (!loading) onCancel();
        }}
      />
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        aria-describedby={description ? "confirm-dialog-desc" : undefined}
        className="relative z-10 w-full max-w-md overflow-hidden rounded-2xl border border-[#E8ECF2] bg-white shadow-[0_24px_64px_rgba(15,23,42,0.22)]"
      >
        <div className="flex gap-4 px-6 pt-6">
          <div
            className={[
              "flex h-11 w-11 shrink-0 items-center justify-center rounded-xl",
              tone === "danger" ? "bg-[#FEF2F2] text-[#DC2626]" : "bg-[#EFF6FF] text-[#2563EB]",
            ].join(" ")}
            aria-hidden
          >
            {tone === "danger" ? <TrashWarnIcon /> : <InfoWarnIcon />}
          </div>
          <div className="min-w-0 flex-1 space-y-1.5 pb-1">
            <h3
              id="confirm-dialog-title"
              className="text-lg font-bold tracking-tight text-[#1A2238]"
            >
              {title}
            </h3>
            {description ? (
              <div id="confirm-dialog-desc" className="text-sm leading-relaxed text-[#6B7280]">
                {description}
              </div>
            ) : null}
          </div>
        </div>

        <div className="mt-6 flex items-center justify-end gap-3 border-t border-[#E8ECF2] bg-[#F7F9FC] px-6 py-4">
          <button
            type="button"
            className={btnCancelClass()}
            disabled={loading}
            onClick={onCancel}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            className={confirmClass}
            disabled={loading}
            onClick={onConfirm}
            autoFocus
          >
            {loading ? "Please wait…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

function TrashWarnIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function InfoWarnIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 8h.01M11 12h1v4h1" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}
