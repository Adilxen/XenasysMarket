"use client";

import {
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import { setProductActive } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/query/keys";
import type { KeysetPageResult, ProductDto } from "@/types/api";

/**
 * Optimistic product Active toggle — UI flips immediately; rolls back on failure.
 */
export function useToggleProductActive() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      setProductActive(id, { isActive }),

    onMutate: async ({ id, isActive }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.products.all });

      const previous = queryClient.getQueriesData<
        InfiniteData<KeysetPageResult<ProductDto>>
      >({ queryKey: queryKeys.products.all });

      queryClient.setQueriesData<InfiniteData<KeysetPageResult<ProductDto>>>(
        { queryKey: queryKeys.products.all },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            pages: old.pages.map((page) => ({
              ...page,
              items: page.items.map((item) =>
                item.id === id ? { ...item, isActive } : item,
              ),
            })),
          };
        },
      );

      return { previous };
    },

    onError: (_err, _vars, context) => {
      context?.previous?.forEach(([key, data]) => {
        queryClient.setQueryData(key, data);
      });
    },

    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.products.all });
    },
  });
}
