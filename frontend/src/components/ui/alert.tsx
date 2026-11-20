/**
 * ==========================================================================================
 * WHAT: Structured dismissible alert banner (UI Validation Plan / T1.1).
 * WHY: Replace floating raw error text with padded icon + bordered system panel.
 * EXAMPLE: <Alert tone="warning" onDismiss={…}>Unable to load roles…</Alert>
 * ==========================================================================================
 */
"use client";

import type { ReactNode } from "react";

export function Alert({
  tone = "error",
  title,
  children,
  onDismiss,
}: {
  tone?: "error" | "warning" | "info" | "success";
  title?: string;
  children: ReactNode;
  onDismiss?: () => void;
}) {
  const styles =
    tone === "error"
      ? {
          wrap: "border-[#FECACA] bg-[#FEF2F2] text-[#991B1B]",
          icon: "text-[#DC2626]",
          title: title ?? "Something went wrong",
        }
      : tone === "warning"
        ? {
            wrap: "border-[#FDE68A] bg-[#FFFBEB] text-[#92400E]",
            icon: "text-[#B45309]",
            title: title ?? "Attention",
          }
        : tone === "success"
          ? {
              wrap: "border-[#BFDBFE] bg-[#EFF6FF] text-[#1E3A8A]",
              icon: "text-[#2563EB]",
              title: title ?? "Saved",
            }
          : {
              wrap: "border-[#BFDBFE] bg-[#EFF6FF] text-[#1E3A8A]",
              icon: "text-[#2563EB]",
              title: title ?? "Notice",
            };

  return (
    <div
      role="alert"
      className={`flex gap-3 rounded-xl border px-4 py-3.5 text-sm shadow-[0_1px_2px_rgba(15,23,42,0.04)] ${styles.wrap}`}
    >
      <span className={`mt-0.5 shrink-0 ${styles.icon}`} aria-hidden>
        {tone === "error" ? <ErrorIcon /> : tone === "warning" ? <WarnIcon /> : <InfoIcon />}
      </span>
      <div className="min-w-0 flex-1 space-y-0.5">
        <p className="font-semibold">{styles.title}</p>
        <div className="leading-relaxed opacity-90">{children}</div>
      </div>
      {onDismiss ? (
        <button
          type="button"
          onClick={onDismiss}
          className="shrink-0 rounded-md p-1 opacity-70 transition hover:bg-black/5 hover:opacity-100"
          aria-label="Dismiss alert"
        >
          <CloseIcon />
        </button>
      ) : null}
    </div>
  );
}

function CloseIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M6 6l12 12M18 6L6 18"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  );
}

function ErrorIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 7.5v6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      <circle cx="12" cy="16.5" r="1" fill="currentColor" />
    </svg>
  );
}

function WarnIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M12 4.5 21 19.5H3L12 4.5Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <path d="M12 10v4.5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      <circle cx="12" cy="17.2" r="1" fill="currentColor" />
    </svg>
  );
}

function InfoIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 10.5V16" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      <circle cx="12" cy="7.8" r="1" fill="currentColor" />
    </svg>
  );
}
