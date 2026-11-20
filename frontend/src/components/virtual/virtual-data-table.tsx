"use client";

import {
  useEffect,
  useRef,
  type ReactNode,
  type CSSProperties,
} from "react";
import { useVirtualizer } from "@tanstack/react-virtual";

export type VirtualColumn<T> = {
  id: string;
  header: string;
  width?: string;
  cell: (row: T) => ReactNode;
};

type VirtualDataTableProps<T> = {
  rows: T[];
  columns: VirtualColumn<T>[];
  getRowId: (row: T) => string | number;
  estimateRowHeight?: number;
  height?: number;
  overscan?: number;
  emptyMessage?: string;
  onEndReached?: () => void;
  endReachedThreshold?: number;
};

/**
 * Viewport-windowed table — only visible rows exist in the DOM.
 * Safe for thousands of catalog / ledger rows without layout thrash.
 */
export function VirtualDataTable<T>({
  rows,
  columns,
  getRowId,
  estimateRowHeight = 44,
  height = 520,
  overscan = 8,
  emptyMessage = "No rows.",
  onEndReached,
  endReachedThreshold = 12,
}: VirtualDataTableProps<T>) {
  const parentRef = useRef<HTMLDivElement>(null);
  const onEndReachedRef = useRef(onEndReached);
  const lastNotifiedLength = useRef(0);

  useEffect(() => {
    onEndReachedRef.current = onEndReached;
  }, [onEndReached]);

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => estimateRowHeight,
    overscan,
  });

  const virtualRows = virtualizer.getVirtualItems();
  const lastIndex = virtualRows.length > 0 ? virtualRows[virtualRows.length - 1]!.index : -1;

  useEffect(() => {
    if (!onEndReachedRef.current || lastIndex < 0) return;
    if (lastIndex < rows.length - endReachedThreshold) return;
    if (lastNotifiedLength.current === rows.length) return;
    lastNotifiedLength.current = rows.length;
    onEndReachedRef.current();
  }, [lastIndex, rows.length, endReachedThreshold]);

  const gridTemplate: CSSProperties = {
    gridTemplateColumns: columns.map((c) => c.width ?? "minmax(0, 1fr)").join(" "),
  };

  if (rows.length === 0) {
    return (
      <div className="flex min-h-[280px] flex-col items-center justify-center rounded-2xl border border-[#E8ECF2] bg-white px-6 py-16 text-center shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-[#EFF6FF] text-[#2563EB]">
          <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden>
            <path d="M21 8.5 12 3 3 8.5v7L12 21l9-5.5v-7z" />
            <path d="M12 12v9" />
            <path d="m3 8.5 9 3.5 9-3.5" />
          </svg>
        </div>
        <p className="text-sm font-semibold text-[#1F2937]">{emptyMessage}</p>
        <p className="mt-1 max-w-sm text-xs leading-relaxed text-[#6B7280]">
          Create your first SKU to populate this virtualized catalog.
        </p>
      </div>
    );
  }

  return (
    <div className="border border-zinc-200 bg-white">
      <div
        className="grid gap-0 border-b border-zinc-200 bg-zinc-50 px-3 py-2 text-left text-xs font-medium uppercase tracking-wide text-zinc-500"
        style={gridTemplate}
      >
        {columns.map((col) => (
          <div key={col.id}>{col.header}</div>
        ))}
      </div>

      <div
        ref={parentRef}
        className="overflow-auto"
        style={{ height }}
        role="grid"
        aria-rowcount={rows.length}
      >
        <div
          style={{
            height: virtualizer.getTotalSize(),
            width: "100%",
            position: "relative",
          }}
        >
          {virtualRows.map((virtualRow) => {
            const row = rows[virtualRow.index];
            if (!row) return null;
            return (
              <div
                key={getRowId(row)}
                role="row"
                className="absolute left-0 top-0 grid w-full items-center gap-0 border-b border-zinc-100 px-3 text-sm hover:bg-zinc-50"
                style={{
                  ...gridTemplate,
                  height: virtualRow.size,
                  transform: `translateY(${virtualRow.start}px)`,
                }}
              >
                {columns.map((col) => (
                  <div key={col.id} role="gridcell" className="truncate pr-2">
                    {col.cell(row)}
                  </div>
                ))}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
