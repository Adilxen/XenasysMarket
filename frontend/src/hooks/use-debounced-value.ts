"use client";

import { useEffect, useState } from "react";

/**
 * Debounce a changing value — isolates rapid keystrokes from expensive consumers
 * (search filters, remote validation) without re-rendering the whole page tree.
 */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(id);
  }, [value, delayMs]);

  return debounced;
}
