"use client";

import { useSuspenseQuery } from "@tanstack/react-query";
import { listCategories } from "@/lib/api/endpoints";
import { MASTER_DATA_GC_MS, MASTER_DATA_STALE_MS } from "@/lib/query/client";
import { queryKeys } from "@/lib/query/keys";

/** Categories / company context — long staleTime avoids redundant API chatter. */
export function useCategoriesSuspense() {
  return useSuspenseQuery({
    queryKey: queryKeys.categories.list(),
    queryFn: listCategories,
    staleTime: MASTER_DATA_STALE_MS,
    gcTime: MASTER_DATA_GC_MS,
  });
}
