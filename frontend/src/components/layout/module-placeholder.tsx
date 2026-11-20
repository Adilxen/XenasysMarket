/**
 * ==========================================================================================
 * WHAT: Lightweight placeholder for Main-screen nav modules not yet built.
 * WHY: Keeps sidebar links routable without 404 while catalog modules land.
 * EXAMPLE: /vendors → calm professional empty state (no dashed “WIP” noise).
 * ==========================================================================================
 */
export function ModulePlaceholder({
  title,
  description,
}: {
  title: string;
  description?: string;
}) {
  return (
    <section className="h-full min-h-0 overflow-y-auto px-5 py-10 lg:px-8 lg:py-12">
      <div className="mx-auto max-w-3xl rounded-2xl border border-[#E8ECF2] bg-white px-6 py-14 text-center shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <p className="text-[11px] font-semibold tracking-[0.16em] text-[#8090A8]">
          MODULE
        </p>
        <h1 className="mt-2 text-2xl font-bold tracking-tight text-[#1A2238]">{title}</h1>
        <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-[#6B7280]">
          {description ??
            "This module is linked from the main navigation. Feature work will land with the matching Task Matrix ID."}
        </p>
      </div>
    </section>
  );
}
