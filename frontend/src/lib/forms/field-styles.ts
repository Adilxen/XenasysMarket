/**
 * ==========================================================================================
 * WHAT: Shared listing + form chrome tokens (Category Listing / Add Category drawer SSoT).
 * WHY: T1.0 / Listing-Form pattern — brand blue accent, #DC2626 errors, zero emerald/green.
 * EXAMPLE: btnPrimaryClass() → Add/Save; btnSecondaryClass() → Export/Cancel; inputClass(err).
 * ==========================================================================================
 */

export const FORM_ERROR_HEX = "#DC2626";
export const ACCENT_HEX = "#2563EB";

const INPUT_BASE =
  "w-full rounded-lg border px-3 py-2.5 text-sm outline-none transition-[border-color,box-shadow,background-color,color] " +
  "placeholder:text-[#9CA3AF] " +
  /* Enabled (interactive) */
  "bg-white text-[#1F2937] shadow-[inset_0_1px_2px_rgba(15,23,42,0.03)] " +
  "hover:border-[#C5CBD6] " +
  /* Disabled — muted lock-in look (View mode / system fields) */
  "disabled:cursor-not-allowed disabled:border-[#E5E7EB] disabled:bg-[#F1F4F9] " +
  "disabled:text-[#64748B] disabled:shadow-none disabled:placeholder:text-[#94A3B8] " +
  "disabled:hover:border-[#E5E7EB] " +
  /* Read-only — soft slate, still readable */
  "read-only:cursor-default read-only:border-[#E8ECF2] read-only:bg-[#F7F9FC] " +
  "read-only:text-[#475569] read-only:shadow-none read-only:hover:border-[#E8ECF2]";

export function inputClass(hasError: boolean, extra = "") {
  return [
    INPUT_BASE,
    hasError
      ? "border-red-600 focus:border-red-600 focus:shadow-[0_0_0_3px_rgba(220,38,38,0.12)] disabled:border-[#FECACA] read-only:border-[#FECACA]"
      : "border-[#D1D5DB] focus:border-[#2563EB] focus:shadow-[0_0_0_3px_rgba(37,99,235,0.12)] focus:bg-white disabled:focus:shadow-none read-only:focus:shadow-none read-only:focus:border-[#E8ECF2]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Soft search / filter inputs (listing toolbar) — light fill, pill-friendly. */
export function searchInputClass(extra = "") {
  return [
    "w-full rounded-full border border-[#E5E7EB] bg-[#F7F8FA] px-4 py-2 pl-10 text-sm text-[#1F2937]",
    "outline-none placeholder:text-[#9CA3AF]",
    "focus:border-[#2563EB] focus:bg-white focus:shadow-[0_0_0_3px_rgba(37,99,235,0.10)]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

export function fieldErrorClass() {
  return "text-xs text-red-600";
}

export function fieldLabelClass(required = false) {
  return [
    "block text-[12.5px] font-semibold text-[#374151]",
    required ? "after:ml-0.5 after:text-red-600 after:content-['*']" : "",
  ]
    .filter(Boolean)
    .join(" ");
}

/** Label tone when the control is disabled / system-locked. */
export function fieldLabelMutedClass(required = false) {
  return [
    "block text-[12.5px] font-semibold text-[#64748B]",
    required ? "after:ml-0.5 after:text-red-600 after:content-['*']" : "",
  ]
    .filter(Boolean)
    .join(" ");
}

/** Primary CTA — Add / Save (Category Listing + drawer). */
export function btnPrimaryClass(extra = "") {
  return [
    "inline-flex items-center justify-center gap-1.5 rounded-lg bg-[#2563EB] px-4 py-2.5 text-sm font-semibold text-white",
    "shadow-[0_3px_10px_rgba(37,99,235,0.25)] transition hover:bg-[#1D4ED8]",
    "disabled:cursor-not-allowed disabled:bg-[#93C5FD] disabled:shadow-none disabled:opacity-100",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Destructive confirm — Delete / Deactivate. */
export function btnDangerClass(extra = "") {
  return [
    "inline-flex items-center justify-center gap-1.5 rounded-lg bg-[#DC2626] px-4 py-2.5 text-sm font-semibold text-white",
    "shadow-[0_3px_10px_rgba(220,38,38,0.22)] transition hover:bg-[#B91C1C]",
    "disabled:cursor-not-allowed disabled:bg-[#FCA5A5] disabled:shadow-none disabled:opacity-100",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Secondary — Export / outline actions. */
export function btnSecondaryClass(extra = "") {
  return [
    "inline-flex items-center justify-center gap-1.5 rounded-lg border border-[#E5E7EB] bg-white px-4 py-2.5 text-sm font-semibold text-[#1F2937]",
    "transition hover:bg-[#F7F9FC]",
    "disabled:cursor-not-allowed disabled:border-[#EEF1F6] disabled:bg-[#F7F9FC] disabled:text-[#94A3B8]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Cancel on drawer footer. */
export function btnCancelClass(extra = "") {
  return btnSecondaryClass(extra);
}

export function btnGhostClass(extra = "") {
  return [
    "inline-flex items-center justify-center rounded-md bg-[#F3F5F9] px-2.5 py-1.5 text-xs font-medium text-[#4B5563]",
    "transition hover:bg-[#E8ECF2]",
    "disabled:opacity-50",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

export function panelClass(extra = "") {
  return [
    "rounded-xl border border-[#E8ECF2] bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.03)]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Fluid tenant canvas — full width beside sidebar; professional gutters only (no max-w-*). */
export function pageCanvasClass(extra = "") {
  return [
    "w-full max-w-[100%] min-w-0 space-y-5 px-4 py-6 sm:px-5 lg:px-6 lg:py-7",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/**
 * Viewport-fill listing canvas — header + card fill main; no page scroll.
 * Pair with DataCard `fill` + DataTable `footerStickyToParent`.
 */
export function pageCanvasFillClass(extra = "") {
  return [
    "flex h-full min-h-0 w-full max-w-[100%] min-w-0 flex-col gap-4 overflow-hidden",
    "px-4 py-4 sm:px-5 lg:px-6 lg:py-5",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Listing toolbar row — wraps on small, single row on lg+. */
export function listingToolbarClass(extra = "") {
  return [
    "flex w-full min-w-0 flex-wrap items-center gap-3 lg:flex-nowrap",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** White listing card wrapping toolbar + table. */
export function dataCardClass(extra = "") {
  return [
    "overflow-hidden rounded-xl border border-[#E8ECF2] bg-white shadow-[0_1px_2px_rgba(15,23,42,0.04)]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

export function pageTitleClass() {
  return "text-2xl font-bold tracking-tight text-[#1A2238]";
}

export function pageSubtitleClass() {
  return "mt-1 text-sm text-[#6B7280]";
}

export function breadcrumbClass() {
  return "text-xs font-medium text-[#8090A8]";
}

export function linkAccentClass() {
  return "text-sm font-medium text-[#2563EB] hover:text-[#1D4ED8] hover:underline";
}

export function tableHeadClass() {
  return "bg-[#F7F9FC] text-[#64748B]";
}

export function tableRowClass() {
  return "border-t border-[#E8ECF2]";
}

export function grantedCellClass() {
  return "bg-[#EFF6FF] text-[#2563EB]";
}

/** Dashed upload zone (Add Category image). */
export function fileDropzoneClass(extra = "") {
  return [
    "flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed border-[#93C5FD] bg-[#EFF6FF]/70 px-4 py-10 text-center",
    "transition hover:bg-[#EFF6FF]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Right drawer shell. */
export function drawerPanelClass(extra = "") {
  return [
    "flex h-full w-full max-w-md flex-col bg-white shadow-[-8px_0_24px_rgba(15,23,42,0.12)]",
    extra,
  ]
    .filter(Boolean)
    .join(" ");
}

export function drawerOverlayClass() {
  return "absolute inset-0 bg-[#0B1B33]/40 backdrop-blur-[1px]";
}

export function drawerFooterClass() {
  return "flex items-center justify-end gap-3 border-t border-[#E8ECF2] bg-white px-5 py-4";
}

export function paginationActiveClass() {
  return "inline-flex h-8 min-w-8 items-center justify-center rounded-md bg-[#2563EB] px-2 text-sm font-semibold text-white";
}

export function paginationIdleClass() {
  return "inline-flex h-8 min-w-8 items-center justify-center rounded-md border border-[#E5E7EB] bg-white px-2 text-sm text-[#6B7280] hover:bg-[#F7F9FC]";
}
