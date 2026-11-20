import type { EntityId } from "@/types/api";

export const queryKeys = {
  tenant: {
    me: ["tenant", "me"] as const,
  },
  categories: {
    all: ["categories"] as const,
    list: () => [...queryKeys.categories.all, "list"] as const,
  },
  products: {
    all: ["products"] as const,
    infinite: (pageSize: number, filterKey = "") =>
      [...queryKeys.products.all, "infinite", pageSize, filterKey] as const,
    detail: (id: EntityId) => [...queryKeys.products.all, "detail", id] as const,
  },
} as const;
