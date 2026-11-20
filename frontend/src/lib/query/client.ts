import { QueryClient } from "@tanstack/react-query";

/**
 * Global TanStack Query defaults tuned for marketplace scale:
 * - Long staleTime for slow-mutating masters (categories, companies)
 * - Shorter staleTime for inventory/ledger streams
 * - gcTime keeps warm cache without unbounded memory growth
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: false,
        retry: 1,
      },
      mutations: {
        retry: 0,
      },
    },
  });
}

/** Slow-mutating dropdown / tenant context — skip redundant roundtrips. */
export const MASTER_DATA_STALE_MS = 30 * 60_000; // 30 min
export const MASTER_DATA_GC_MS = 60 * 60_000; // 1 hr

/** Hot inventory / ledger streams — fresher, still cached between navigations. */
export const STREAM_DATA_STALE_MS = 15_000;
export const STREAM_DATA_GC_MS = 10 * 60_000;
