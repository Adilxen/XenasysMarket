"use client";

/**
 * ==========================================================================================
 * WHAT: Modal when user opens a Form path without RoleFormActions grant.
 * WHY: URL-bar / deep-link attempts must show explicit restrict UX (not silent redirect).
 * EXAMPLE: Member opens /settings/forms → "Access restricted" → OK → Dashboard.
 * ==========================================================================================
 */

import { useEffect } from "react";
import { btnPrimaryClass, drawerOverlayClass } from "@/lib/forms/field-styles";

export function AccessRestrictedDialog({
  open,
  screenName,
  onAcknowledge,
}: {
  open: boolean;
  screenName?: string;
  onAcknowledge: () => void;
}) {
  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onAcknowledge();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onAcknowledge]);

  if (!open) return null;

  const detail = screenName?.trim()
    ? `You do not have permission to open “${screenName.trim()}”.`
    : "You do not have permission to open this screen.";

  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center p-4">
      <button
        type="button"
        aria-label="Dismiss"
        className={drawerOverlayClass()}
        onClick={onAcknowledge}
      />
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="access-restricted-title"
        aria-describedby="access-restricted-desc"
        className="relative z-10 w-full max-w-md overflow-hidden rounded-2xl border border-[#E8ECF2] bg-white shadow-[0_24px_64px_rgba(15,23,42,0.22)]"
      >
        <div className="flex gap-4 px-6 pt-6">
          <div
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#FEF3C7] text-[#B45309]"
            aria-hidden
          >
            <LockIcon />
          </div>
          <div className="min-w-0 flex-1 space-y-1.5 pb-1">
            <h3
              id="access-restricted-title"
              className="text-lg font-bold tracking-tight text-[#1A2238]"
            >
              Access restricted
            </h3>
            <p id="access-restricted-desc" className="text-sm leading-relaxed text-[#6B7280]">
              {detail} Contact your company administrator if you need access.
            </p>
          </div>
        </div>

        <div className="mt-6 flex items-center justify-end border-t border-[#E8ECF2] bg-[#F7F9FC] px-6 py-4">
          <button
            type="button"
            className={btnPrimaryClass()}
            onClick={onAcknowledge}
            autoFocus
          >
            OK
          </button>
        </div>
      </div>
    </div>
  );
}

function LockIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <rect
        x="5"
        y="11"
        width="14"
        height="10"
        rx="2"
        stroke="currentColor"
        strokeWidth="1.8"
      />
      <path
        d="M8 11V8a4 4 0 0 1 8 0v3"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  );
}
