"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  flattenProductPages,
  useProductsInfinite,
} from "@/hooks/use-products-infinite";
import {
  VirtualDataTable,
  type VirtualColumn,
} from "@/components/virtual/virtual-data-table";
import { ProductActiveToggle } from "@/components/products/product-active-toggle";
import type { ProductDto } from "@/types/api";

export function ProductCatalogVirtual() {
  const searchParams = useSearchParams();
  const qFromUrl = (searchParams.get("q") ?? "").trim();

  const [draftCode, setDraftCode] = useState("");
  const [draftName, setDraftName] = useState(qFromUrl);
  const [filters, setFilters] = useState<Record<string, string>>(() =>
    qFromUrl ? ({ productName: qFromUrl } as Record<string, string>) : {},
  );

  useEffect(() => {
    if (!qFromUrl) return;
    setDraftName(qFromUrl);
  }, [qFromUrl]);

  useEffect(() => {
    const t = setTimeout(() => {
      const next: Record<string, string> = {};
      const code = draftCode.trim();
      const name = draftName.trim();
      if (code) next.productCode = draftCode;
      if (name) next.productName = draftName;
      setFilters(next);
    }, 300);
    return () => clearTimeout(t);
  }, [draftCode, draftName]);

  const { data, isFetchingNextPage, hasNextPage, fetchNextPage } =
    useProductsInfinite(filters);

  const rows = useMemo(() => flattenProductPages(data), [data]);

  const onEndReached = useCallback(() => {
    if (hasNextPage && !isFetchingNextPage) {
      void fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const columns = useMemo<VirtualColumn<ProductDto>[]>(
    () => [
      {
        id: "productCode",
        header: "Product code",
        width: "minmax(9rem, 1.4fr)",
        cell: (p) => (
          <span className="font-mono text-xs">{p.productCode}</span>
        ),
      },
      {
        id: "name",
        header: "Name",
        width: "minmax(10rem, 2fr)",
        cell: (p) => p.productName,
      },
      {
        id: "retail",
        header: "Retail",
        width: "6rem",
        cell: (p) => p.retailPrice.toFixed(2),
      },
      {
        id: "stock",
        header: "Min Stock",
        width: "5.5rem",
        cell: (p) => p.minimumStockLevel,
      },
      {
        id: "active",
        header: "Status",
        width: "5.5rem",
        cell: (p) => (
          <ProductActiveToggle productId={p.id} isActive={p.isActive} />
        ),
      },
    ],
    [],
  );

  return (
    <div className="space-y-2">
      <div className="grid gap-2 sm:grid-cols-2">
        <label className="flex flex-col gap-1 text-xs font-medium text-[#64748B]">
          Product code
          <input
            type="search"
            className="rounded-md border border-[#E8ECF2] px-3 py-2 text-sm text-[#1A2238] outline-none focus:border-[#2563EB]"
            placeholder="Contains…"
            value={draftCode}
            onChange={(e) => setDraftCode(e.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-[#64748B]">
          Name
          <input
            type="search"
            className="rounded-md border border-[#E8ECF2] px-3 py-2 text-sm text-[#1A2238] outline-none focus:border-[#2563EB]"
            placeholder="Contains…"
            value={draftName}
            onChange={(e) => setDraftName(e.target.value)}
          />
        </label>
      </div>
      <VirtualDataTable
        rows={rows}
        columns={columns}
        getRowId={(p) => p.id}
        onEndReached={onEndReached}
        emptyMessage="No products yet."
        height={560}
      />
      {isFetchingNextPage ? (
        <p className="text-xs text-zinc-500">Streaming next keyset page…</p>
      ) : null}
    </div>
  );
}
