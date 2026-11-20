/**
 * ==========================================================================================
 * WHAT: Slim main-screen footer bar — XENASYS Development credit in one compact row.
 * WHY: Low visual weight under the canvas; keeps attribution without eating content height.
 * EXAMPLE: "DEVELOPED BY · XENASYS Development PVT" + short tagline on the right.
 * ==========================================================================================
 */
export function AppFooter() {
  return (
    <footer className="shrink-0 border-t border-[#EEF1F6] bg-gradient-to-r from-[#F8F9FB] via-white to-[#F8F9FB] px-4 py-1.5 lg:px-6">
      <div className="flex h-7 items-center justify-between gap-3">
        <p className="flex min-w-0 items-center gap-2 text-[11px] leading-none">
          <span className="font-semibold tracking-[0.14em] text-[#9AA3B2]">
            DEVELOPED BY
          </span>
          <span className="hidden h-3 w-px bg-[#E5E7EB] sm:block" aria-hidden />
          <span className="truncate font-semibold tracking-wide text-[#1A2238]">
            XENASYS{" "}
            <span className="font-medium text-[#6B7280]">Development PVT</span>
          </span>
        </p>

        <p className="hidden truncate text-[9px] font-medium uppercase tracking-[0.16em] text-[#A0A8B8] md:block">
          Building Solutions For A Smarter Tomorrow
        </p>
      </div>
    </footer>
  );
}
