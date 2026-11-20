"use client";

import { useEffect, useMemo, useRef } from "react";

/**
 * Stable debounced callback — parent can pass this to encapsulated fields
 * without causing the parent to re-render on every keypress.
 */
export function useDebouncedCallback<TArgs extends unknown[]>(
  callback: (...args: TArgs) => void,
  delayMs = 300,
): (...args: TArgs) => void {
  const callbackRef = useRef(callback);
  const timerRef = useRef<number | null>(null);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
      }
    };
  }, []);

  return useMemo(
    () =>
      (...args: TArgs) => {
        if (timerRef.current !== null) {
          window.clearTimeout(timerRef.current);
        }
        timerRef.current = window.setTimeout(() => {
          callbackRef.current(...args);
        }, delayMs);
      },
    [delayMs],
  );
}
