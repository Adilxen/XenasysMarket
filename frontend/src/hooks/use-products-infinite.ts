"use client";

import {
  useSuspenseInfiniteQuery,
  type InfiniteData,
} from "@tanstack/react-query";
import { listProducts } from "@/lib/api/endpoints";
import { STREAM_DATA_GC_MS, STREAM_DATA_STALE_MS } from "@/lib/query/client";
import { queryKeys } from "@/lib/query/keys";
import type { KeysetPageResult, ProductDto } from "@/types/api";

const PAGE_SIZE = 100;

export function useProductsInfinite(filters: Record<string, string> = {}) {
  const filterKey = JSON.stringify(filters);
  return useSuspenseInfiniteQuery<
    KeysetPageResult<ProductDto>,
    Error,
    InfiniteData<KeysetPageResult<ProductDto>>,
    ReturnType<typeof queryKeys.products.infinite>,
    number | null
  >({
    queryKey: queryKeys.products.infinite(PAGE_SIZE, filterKey),
    queryFn: ({ pageParam }) =>
      listProducts({
        afterId: pageParam,
        limit: PAGE_SIZE,
        filters,
      }),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => (last.hasMore ? last.nextCursor : undefined),
    staleTime: STREAM_DATA_STALE_MS,
    gcTime: STREAM_DATA_GC_MS,
  });
}

export function flattenProductPages(
  data: InfiniteData<KeysetPageResult<ProductDto>>,
): ProductDto[] {
  return data.pages.flatMap((p) => p.items);
}
