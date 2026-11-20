"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";

/**
 * ==========================================================================================
 * WHAT: Column filter state for T1.52 server-side DataTable Contains filters.
 * WHY: Funnel debounce lives in SpreadsheetGrid; pages hold the committed filter map for API.
 * EXAMPLE: useServerColumnFilters({ seedFromQuery: "name" }) — Global Search ?q= → name filter.
 * ==========================================================================================
 */
export function useServerColumnFilters(options?: {
  /**
   * Global Search Center (T1.53): when URL has `?q=…`, seed/sync that value into this
   * column filter key (e.g. company `name`, users `userName`, roles `roleCode`).
   */
  seedFromQuery?: string;
}) {
  const searchParams = useSearchParams();
  const seedKey = options?.seedFromQuery;
  const qFromUrl = (searchParams.get("q") ?? "").trim();

  const [columnFilters, setColumnFilters] = useState<Record<string, string>>(() =>
    seedKey && qFromUrl ? { [seedKey]: qFromUrl } : {},
  );

  useEffect(() => {
    if (!seedKey) return;
    const nextQ = (searchParams.get("q") ?? "").trim();
    if (!nextQ) return;
    setColumnFilters((prev) => {
      if (prev[seedKey] === nextQ) return prev;
      return { [seedKey]: nextQ };
    });
  }, [searchParams, seedKey]);

  const onColumnFilterChange = useCallback((key: string, value: string) => {
    setColumnFilters((prev) => {
      const next = { ...prev };
      const trimmed = value.trim();
      if (!trimmed) {
        delete next[key];
      } else {
        next[key] = value;
      }
      return next;
    });
  }, []);

  return { columnFilters, onColumnFilterChange, setColumnFilters };
}

/** Append non-empty filter entries onto URLSearchParams (query keys = map keys). */
export function appendColumnFilters(
  params: URLSearchParams,
  filters?: Record<string, string> | null,
): void {
  if (!filters) return;
  for (const [key, raw] of Object.entries(filters)) {
    const value = raw.trim();
    if (value) params.set(key, value);
  }
}
