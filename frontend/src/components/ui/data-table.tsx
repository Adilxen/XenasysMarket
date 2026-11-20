"use client";

/**
 * ==========================================================================================
 * WHAT: Listing DataTable — SpreadsheetGrid (jspreadsheet-ce) wrapper from newSalon.
 * WHY: Same fast listing UX as glam-loft; React portals for custom cells/actions.
 * EXAMPLE: <DataTable columns={…} data={rows} getRowKey={(r) => r.id} />
 * ==========================================================================================
 */

import {
  isValidElement,
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { ChevronLeft, ChevronRight } from "lucide-react";
import SpreadsheetGrid, {
  type SpreadsheetCellValue,
  type SpreadsheetColumnDef,
  type SpreadsheetFooters,
} from "@/components/ui/spreadsheet-grid";
import { btnSecondaryClass } from "@/lib/forms/field-styles";
import { formatHeadingTitle } from "@/lib/utils/to-title-case";

/** Default Actions column width (~3 icon slots), matching Category Listing. */
export const TABLE_ROW_ACTIONS_DEFAULT_COLUMN_WIDTH = 3 * 36 + 16;

/**
 * Viewport-tied listing body height (fallback when not using footerStickyToParent).
 * Prefer footerStickyToParent + DataCard fill for screen-locked listing pages.
 */
export const LISTING_TABLE_VIEWPORT_HEIGHT =
  "min-h-0 max-h-full";

export interface DataTableColumn<T> {
  key: string;
  label: string;
  width?: number | string;
  grow?: boolean;
  headerContent?: ReactNode;
  render?: (row: T) => ReactNode;
  headerClassName?: string;
  cellClassName?: string;
  /** When false, hide funnel for this column (default true when table filters on). */
  filter?: boolean;
  /** Server filter query key when different from `key`. */
  filterKey?: string;
}

export interface DataTablePagination {
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  pageSizeOptions?: number[];
  onPageSizeChange: (size: number) => void;
  onPageChange: (page: number) => void;
}

const DEFAULT_PAGE_SIZE_OPTIONS = [5, 10, 15, 20, 50];

function escapeAttr(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

export interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  data: T[];
  getRowKey: (row: T) => string | number;
  /** Prefer false for snappy UX — keep prior rows visible while refetching. */
  loading?: boolean;
  emptyMessage?: string;
  pagination?: DataTablePagination;
  renderActions?: (row: T) => ReactNode;
  actionsColumnLabel?: string;
  actionsColumnWidth?: number;
  actionsHeaderClassName?: string;
  actionsCellClassName?: string;
  maxHeight?: string;
  /** Default false in Xenasys — never blank the grid for a loading flash. */
  hideTableWhenLoading?: boolean;
  tableWrapperClassName?: string;
  tableClassName?: string;
  footerStickyToParent?: boolean;
  paginationFooterClassName?: string;
  variant?: "default" | "spreadsheet";
  filters?: boolean;
  /** T1.52 — funnel text → server Contains (parent refetches). */
  serverFilters?: boolean;
  columnFilters?: Record<string, string>;
  onColumnFilterChange?: (key: string, value: string) => void;
  footers?: SpreadsheetFooters;
}

type SheetRow<T> = {
  _row: T;
  _key: string | number;
  _filter: Record<string, SpreadsheetCellValue>;
};

function getRawCellValue(row: Record<string, unknown>, key: string): unknown {
  const value = row[key];
  return value === undefined || value === null ? "—" : value;
}

function toFilterValue(value: unknown): SpreadsheetCellValue {
  if (value === undefined || value === null) return "—";
  if (typeof value === "boolean") return value ? "Active" : "Inactive";
  if (typeof value === "string" || typeof value === "number") {
    return value;
  }
  return String(value);
}

function normalizeDataTableFilterValue(
  colKey: string,
  value: SpreadsheetCellValue,
): SpreadsheetCellValue {
  if (typeof value !== "string") return value;
  const key = colKey.toLowerCase();
  if (/time|datetime/.test(key)) return value;
  if (/^\d{4}-\d{2}-\d{2}/.test(value)) return value.slice(0, 10);
  return value;
}

function deriveFilterValue<T extends object>(
  row: T,
  col: DataTableColumn<T>,
): SpreadsheetCellValue {
  if (col.render) {
    const rendered = col.render(row);
    if (typeof rendered === "string" || typeof rendered === "number" || typeof rendered === "boolean") {
      return normalizeDataTableFilterValue(col.key, rendered);
    }
  }

  const raw = (row as Record<string, unknown>)[col.key];
  if (raw !== undefined && raw !== null && raw !== "") {
    return normalizeDataTableFilterValue(col.key, toFilterValue(raw));
  }
  if (raw === undefined || raw === null) return "—";
  return normalizeDataTableFilterValue(col.key, toFilterValue(raw));
}

function needsReactMount(node: ReactNode): boolean {
  if (node == null || typeof node === "boolean") return false;
  if (typeof node === "string" || typeof node === "number") return false;
  if (isValidElement(node)) return true;
  if (Array.isArray(node)) return node.some(needsReactMount);
  return false;
}

function ensureCellMountPoint(
  cell: HTMLTableCellElement,
  rowKey: string | number,
  colKey: string,
  cellClassName?: string,
): void {
  if (cellClassName) cell.classList.add(cellClassName);

  let mount = cell.querySelector(":scope > .sg-dt-cell-mount") as HTMLElement | null;
  if (!mount) {
    while (cell.firstChild) cell.removeChild(cell.firstChild);
    mount = document.createElement("div");
    mount.className = "sg-dt-cell-mount";
    mount.style.display = "flex";
    mount.style.alignItems = "center";
    mount.style.justifyContent = "flex-start";
    mount.style.width = "100%";
    mount.style.minWidth = "0";
    mount.style.maxWidth = "100%";
    mount.style.overflow = "hidden";
    cell.appendChild(mount);
  }

  mount.setAttribute("data-dt-row", String(rowKey));
  mount.setAttribute("data-dt-col", colKey);
}

function DataTablePaginationFooter({
  pag,
  pageSizeOptions,
  className,
}: {
  pag: DataTablePagination;
  pageSizeOptions: number[];
  className: string;
}) {
  return (
    <div className={className}>
      <div className="flex flex-wrap items-center gap-3 sm:gap-4">
        <span className="text-xs text-[#6B7280] sm:text-sm">
          Total: <span className="font-medium text-[#1A2238]">{pag.totalCount}</span>
        </span>
        <label className="flex items-center gap-2 text-xs text-[#6B7280] sm:text-sm">
          Per page
          <select
            value={pag.pageSize}
            onChange={(e) => pag.onPageSizeChange(Number(e.target.value))}
            className="rounded-md border border-[#E5E7EB] bg-white px-2 py-1 text-xs text-[#1A2238] sm:text-sm"
          >
            {pageSizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="flex items-center justify-center gap-2 sm:justify-end">
        <button
          type="button"
          className={btnSecondaryClass("flex-1 px-3 py-1.5 text-xs sm:flex-initial")}
          onClick={() => pag.onPageChange(pag.pageNumber - 1)}
          disabled={pag.pageNumber <= 1}
        >
          <ChevronLeft className="h-4 w-4 sm:mr-1" />
          <span className="hidden sm:inline">Previous</span>
        </button>
        <span className="shrink-0 px-1 text-xs text-[#6B7280] sm:px-2 sm:text-sm">
          {pag.pageNumber}/{pag.totalPages || 1}
        </span>
        <button
          type="button"
          className={btnSecondaryClass("flex-1 px-3 py-1.5 text-xs sm:flex-initial")}
          onClick={() => pag.onPageChange(pag.pageNumber + 1)}
          disabled={pag.pageNumber >= pag.totalPages}
        >
          <span className="hidden sm:inline">Next</span>
          <ChevronRight className="h-4 w-4 sm:ml-1" />
        </button>
      </div>
    </div>
  );
}

function SpreadsheetDataTable<T extends object>({
  columns,
  data,
  getRowKey,
  loading = false,
  emptyMessage = "No data found.",
  pagination,
  renderActions,
  actionsColumnLabel = "Actions",
  actionsColumnWidth = TABLE_ROW_ACTIONS_DEFAULT_COLUMN_WIDTH,
  maxHeight = LISTING_TABLE_VIEWPORT_HEIGHT,
  hideTableWhenLoading = false,
  tableWrapperClassName,
  footerStickyToParent = false,
  paginationFooterClassName: paginationFooterClassNameProp,
  filters = true,
  serverFilters = false,
  columnFilters,
  onColumnFilterChange,
  footers,
}: DataTableProps<T>) {
  const gridWrapRef = useRef<HTMLDivElement>(null);
  const [actionMounts, setActionMounts] = useState<
    { key: string; el: HTMLElement; row: T }[]
  >([]);
  const [cellMounts, setCellMounts] = useState<
    { key: string; el: HTMLElement; row: T; colKey: string }[]
  >([]);

  const sheetRows = useMemo<SheetRow<T>[]>(
    () =>
      data.map((row) => {
        const filter: Record<string, SpreadsheetCellValue> = {};
        for (const col of columns) {
          filter[col.key] = deriveFilterValue(row, col);
        }
        return {
          _row: row,
          _key: getRowKey(row),
          _filter: filter,
        };
      }),
    [columns, data, getRowKey],
  );

  const sheetColumns = useMemo<SpreadsheetColumnDef<SheetRow<T>>[]>(
    () =>
      columns.map((col) => ({
        key: col.key,
        title: formatHeadingTitle(col.label),
        type: "text" as const,
        width: col.width,
        grow: col.grow,
        readOnly: true,
        filter: col.filter !== false,
        filterKey: col.filterKey,
        align: "left" as const,
        getValue: (row) => row._filter[col.key] ?? "—",
        render: (cell, value, row) => {
          if (!col.render) {
            cell.classList.remove("sg-dt-react-cell");
            cell.textContent = value == null ? "—" : String(value);
            return;
          }
          const rendered = col.render(row._row);
          if (!needsReactMount(rendered)) {
            cell.classList.remove("sg-dt-react-cell");
            cell.textContent =
              rendered == null || rendered === false ? String(value ?? "—") : String(rendered);
            return;
          }
          ensureCellMountPoint(cell, row._key, col.key, "sg-dt-react-cell");
        },
      })),
    [columns],
  );

  const gridActions = useMemo(
    () =>
      renderActions
        ? {
            title: formatHeadingTitle(actionsColumnLabel),
            width: actionsColumnWidth,
            grow: false as const,
            align: "center" as const,
            renderHtml: (row: SheetRow<T>) =>
              `<div class="sg-dt-actions-mount" data-dt-key="${escapeAttr(String(row._key))}"></div>`,
          }
        : undefined,
    [actionsColumnLabel, actionsColumnWidth, renderActions],
  );

  const syncPortalMounts = useCallback(() => {
    const root = gridWrapRef.current;
    if (!root) {
      setActionMounts((prev) => (prev.length === 0 ? prev : []));
      setCellMounts((prev) => (prev.length === 0 ? prev : []));
      return;
    }

    const nextActions: { key: string; el: HTMLElement; row: T }[] = [];
    if (renderActions) {
      root.querySelectorAll<HTMLElement>(".sg-dt-actions-mount[data-dt-key]").forEach((el) => {
        if (!el.isConnected) return;
        const key = el.getAttribute("data-dt-key") ?? "";
        if (!key) return;
        el.closest("td")?.classList.add("sg-dt-actions-cell");
        const row = data.find((r) => String(getRowKey(r)) === key);
        if (row) nextActions.push({ key, el, row });
      });
    }

    setActionMounts((prev) => {
      if (
        prev.length === nextActions.length &&
        prev.every((p, i) => {
          const n = nextActions[i];
          if (!n || p.key !== n.key || p.el !== n.el || p.row !== n.row) return false;
          if (!n.el.childNodes.length) return false;
          return true;
        })
      ) {
        return prev;
      }
      return nextActions;
    });

    const nextCells: { key: string; el: HTMLElement; row: T; colKey: string }[] = [];
    root.querySelectorAll<HTMLElement>(".sg-dt-cell-mount[data-dt-row][data-dt-col]").forEach((el) => {
      if (!el.isConnected) return;
      const rowKey = el.getAttribute("data-dt-row") ?? "";
      const colKey = el.getAttribute("data-dt-col") ?? "";
      if (!rowKey || !colKey) return;
      const row = data.find((r) => String(getRowKey(r)) === rowKey);
      if (row) nextCells.push({ key: `${rowKey}::${colKey}`, el, row, colKey });
    });

    setCellMounts((prev) => {
      if (
        prev.length === nextCells.length &&
        prev.every((p, i) => {
          const n = nextCells[i];
          if (
            !n ||
            p.key !== n.key ||
            p.el !== n.el ||
            p.colKey !== n.colKey ||
            p.row !== n.row
          ) {
            return false;
          }
          if (!n.el.childNodes.length) return false;
          return true;
        })
      ) {
        return prev;
      }
      return nextCells;
    });
  }, [data, getRowKey, renderActions]);

  useLayoutEffect(() => {
    if (loading && hideTableWhenLoading) {
      setActionMounts((prev) => (prev.length === 0 ? prev : []));
      setCellMounts((prev) => (prev.length === 0 ? prev : []));
      return;
    }

    syncPortalMounts();
    const root = gridWrapRef.current;
    if (!root) return;

    let frame = 0;
    const scheduleSync = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => syncPortalMounts());
    };
    const obs = new MutationObserver(scheduleSync);
    obs.observe(root, { childList: true, subtree: true });

    // CE paints action HTML after React layout — rAF + one microtask retry only (no 200/400ms lag).
    const t0 = window.setTimeout(syncPortalMounts, 0);

    return () => {
      cancelAnimationFrame(frame);
      obs.disconnect();
      window.clearTimeout(t0);
    };
  }, [hideTableWhenLoading, loading, sheetRows, syncPortalMounts]);

  const columnsByKey = useMemo(() => {
    const map = new Map<string, DataTableColumn<T>>();
    for (const col of columns) map.set(col.key, col);
    return map;
  }, [columns]);

  const cellPortals = cellMounts.map(({ key, el, row, colKey }) => {
    const col = columnsByKey.get(colKey);
    if (!col?.render) return null;
    return createPortal(<>{col.render(row)}</>, el, key);
  });

  const actionPortals =
    renderActions &&
    actionMounts.map(({ key, el, row }) =>
      createPortal(
        <div className="flex w-full flex-nowrap items-center justify-center gap-1">
          {renderActions(row)}
        </div>,
        el,
        key,
      ),
    );

  const pag = pagination;
  const pageSizeOptions = pag?.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS;
  const showPagination = Boolean(pag && pag.totalCount > 0 && !(loading && hideTableWhenLoading));
  const showEmpty = !loading && data.length === 0;

  const paginationFooterClassName =
    paginationFooterClassNameProp ??
    (footerStickyToParent
      ? "shrink-0 flex flex-col sm:flex-row flex-wrap items-stretch sm:items-center justify-between gap-3 px-3 sm:px-4 py-3 border-t border-[#E8ECF2] bg-[#F7F9FC]"
      : "flex flex-col sm:flex-row flex-wrap items-stretch sm:items-center justify-between gap-3 px-3 sm:px-4 py-3 border-t border-[#E8ECF2] bg-[#F7F9FC]");

  const paginationFooter =
    showPagination && pag ? (
      <DataTablePaginationFooter
        pag={pag}
        pageSizeOptions={pageSizeOptions}
        className={paginationFooterClassName}
      />
    ) : null;

  if (loading && hideTableWhenLoading) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-[#8090A8]">
        Loading…
      </div>
    );
  }

  const spreadsheet = (
    <SpreadsheetGrid<SheetRow<T>>
      className={
        footerStickyToParent
          ? "min-h-0 min-w-0 h-full w-full flex-1"
          : "sg-root-fit-content min-h-0 min-w-0 w-full"
      }
      columns={sheetColumns}
      data={sheetRows}
      actions={gridActions}
      filters={filters}
      serverFilters={serverFilters}
      columnFilters={columnFilters}
      onColumnFilterChange={onColumnFilterChange}
      footers={footers}
      disabled={false}
      allowInsertRow={false}
      allowDeleteRow={false}
      columnSorting={false}
      columnDrag={false}
      rowDrag={false}
      columnResize
      rowResize={false}
      selectionCopy
      lockReadonlyInteraction={false}
      emptyMessage={emptyMessage}
    />
  );

  const grid = showEmpty ? (
    <div
      className={
        footerStickyToParent
          ? "flex min-h-0 flex-1 flex-col items-center justify-center px-4 py-8 sm:py-10"
          : "px-4 py-8 text-center sm:py-12"
      }
    >
      <p className="text-center text-sm text-[#8090A8] sm:text-base">{emptyMessage}</p>
    </div>
  ) : footerStickyToParent ? (
    <div
      ref={gridWrapRef}
      className={`flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden ${tableWrapperClassName ?? ""}`.trim()}
    >
      {spreadsheet}
      {cellPortals}
      {actionPortals}
    </div>
  ) : (
    <div
      ref={gridWrapRef}
      className={`flex min-h-0 min-w-0 flex-col overflow-x-auto overflow-y-auto ${maxHeight} ${tableWrapperClassName ?? ""}`.trim()}
    >
      {spreadsheet}
      {cellPortals}
      {actionPortals}
    </div>
  );

  if (footerStickyToParent) {
    return (
      <div className="flex h-full min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden">
        <div className="flex min-h-0 min-w-0 flex-1 basis-0 flex-col overflow-hidden">{grid}</div>
        {paginationFooter}
      </div>
    );
  }

  return (
    <>
      {grid}
      {paginationFooter}
    </>
  );
}

function HtmlDataTable<T extends object>({
  columns,
  data,
  getRowKey,
  loading = false,
  emptyMessage = "No data found.",
  pagination,
  renderActions,
  actionsColumnLabel = "Actions",
  actionsHeaderClassName,
  actionsCellClassName,
  maxHeight = LISTING_TABLE_VIEWPORT_HEIGHT,
  hideTableWhenLoading = false,
  tableWrapperClassName,
  tableClassName,
  footerStickyToParent = false,
  paginationFooterClassName: paginationFooterClassNameProp,
}: DataTableProps<T>) {
  const showTable = !loading || !hideTableWhenLoading;
  const showEmpty = !loading && data.length === 0;
  const pag = pagination;
  const pageSizeOptions = pag?.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS;
  const showPagination = pag && pag.totalCount > 0;

  const tableScrollAreaClass = footerStickyToParent
    ? `flex min-h-0 min-w-0 flex-1 basis-0 flex-col overflow-x-auto overflow-y-auto ${tableWrapperClassName ?? ""}`.trim()
    : `overflow-x-auto overflow-y-auto ${maxHeight} ${tableWrapperClassName ?? ""}`.trim();

  const paginationFooterClassName =
    paginationFooterClassNameProp ??
    "flex flex-col sm:flex-row flex-wrap items-stretch sm:items-center justify-between gap-3 px-3 sm:px-4 py-3 border-t border-[#E8ECF2] bg-[#F7F9FC]";

  const thBase = "px-4 py-3 text-left text-xs font-semibold text-[#64748B]";
  const tdBase = "px-4 py-3.5 text-sm text-[#1A2238]";

  const paginationFooter =
    showPagination && !(loading && hideTableWhenLoading) && pag ? (
      <DataTablePaginationFooter
        pag={pag}
        pageSizeOptions={pageSizeOptions}
        className={paginationFooterClassName}
      />
    ) : null;

  const tableBody = (
    <>
      {loading && hideTableWhenLoading ? (
        <div className="flex items-center justify-center py-12 text-sm text-[#8090A8]">
          Loading…
        </div>
      ) : (
        showTable && (
          <table className={`w-max min-w-full ${tableClassName ?? ""}`.trim()}>
            <thead className="sticky top-0 z-10 bg-[#F7F9FC]">
              <tr>
                {columns.map((col) => (
                  <th key={col.key} className={`${thBase} ${col.headerClassName ?? ""}`.trim()}>
                    {col.headerContent ?? formatHeadingTitle(col.label)}
                  </th>
                ))}
                {renderActions ? (
                  <th className={`${thBase} text-center ${actionsHeaderClassName ?? ""}`.trim()}>
                    {formatHeadingTitle(actionsColumnLabel)}
                  </th>
                ) : null}
              </tr>
            </thead>
            <tbody>
              {data.map((row) => (
                <tr key={String(getRowKey(row))} className="border-t border-[#E8ECF2]">
                  {columns.map((col) => (
                    <td key={col.key} className={`${tdBase} ${col.cellClassName ?? ""}`.trim()}>
                      {col.render
                        ? col.render(row)
                        : String(getRawCellValue(row as Record<string, unknown>, col.key))}
                    </td>
                  ))}
                  {renderActions ? (
                    <td className={`${tdBase} ${actionsCellClassName ?? ""}`.trim()}>
                      <div className="flex min-w-0 flex-nowrap items-center justify-center gap-1">
                        {renderActions(row)}
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        )
      )}
    </>
  );

  const content = (
    <>
      <div className={tableScrollAreaClass}>
        {tableBody}
        {showEmpty && footerStickyToParent ? (
          <div className="flex min-h-0 flex-1 flex-col items-center justify-center px-4 py-8">
            <p className="text-sm text-[#8090A8]">{emptyMessage}</p>
          </div>
        ) : null}
      </div>
      {showEmpty && !footerStickyToParent ? (
        <div className="px-4 py-8 text-center">
          <p className="text-sm text-[#8090A8]">{emptyMessage}</p>
        </div>
      ) : null}
      {!footerStickyToParent && paginationFooter}
    </>
  );

  if (footerStickyToParent) {
    return (
      <div className="flex h-full min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden">
        {content}
        {paginationFooter}
      </div>
    );
  }

  return content;
}

export default function DataTable<T extends object>(props: DataTableProps<T>) {
  const variant = props.variant ?? "spreadsheet";
  if (variant === "spreadsheet") {
    return <SpreadsheetDataTable {...props} filters={props.filters ?? true} />;
  }
  return <HtmlDataTable {...props} />;
}
