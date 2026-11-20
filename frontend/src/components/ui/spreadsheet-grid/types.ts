import type { ReactNode, RefObject } from 'react';

export type SpreadsheetCellValue = string | number | boolean | null | undefined;

/** Loose typing for jspreadsheet CE instances (package uses `export =`). */
export type SpreadsheetWorksheetInstance = {
  getData: (processed?: boolean) => unknown;
  setData: (data?: unknown) => void;
  setWidth?: (column: number, width: number) => void;
  closeEditor?: (cell: HTMLTableCellElement, save: boolean) => void;
  resetSelection?: () => 0 | 1;
  updateSelectionFromCoords?: (
    x1: number | null,
    y1: number | null,
    x2?: number | null,
    y2?: number | null
  ) => false | undefined;
  getSelection?: () => [number, number, number, number];
  getSelected?: (columnNameOnly?: boolean, returnObjects?: boolean) => unknown;
  getCell?: (x: number, y: number) => HTMLTableCellElement;
  getCellFromCoords?: (x: number, y: number) => HTMLTableCellElement;
  getValue?: (x: string | number, y: string | number) => SpreadsheetCellValue;
  setStyle?: (
    cell: string | HTMLElement | unknown,
    property?: string,
    value?: string,
    forceOverwrite?: boolean
  ) => void;
  orderBy?: (column: number, order?: number) => unknown;
  openFilter?: (columnIndex: number) => void;
  resetFilters?: () => void;
  search?: (query: string) => void;
  resetSearch?: () => void;
  undo?: () => void;
  redo?: () => void;
  copy?: (cut?: boolean) => void;
  cut?: () => void;
  paste?: () => void;
  download?: (includeHeaders?: boolean) => void;
  element?: HTMLElement;
  table?: HTMLTableElement;
  options?: { editable?: boolean; footers?: string[][]; filters?: boolean; [key: string]: unknown };
  [key: string]: unknown;
};

export type SpreadsheetColumnType =
  | 'text'
  | 'numeric'
  | 'hidden'
  | 'dropdown'
  | 'autocomplete'
  | 'checkbox'
  | 'radio'
  | 'calendar'
  | 'image'
  | 'color'
  | 'html';

export type SpreadsheetAlign = 'left' | 'center' | 'right' | 'justify';

export type SpreadsheetDropdownSourceItem =
  | string
  | number
  | {
      id: string | number;
      name: string;
      title?: string;
      image?: string;
      group?: string;
    };

/** jspreadsheet-ce toolbar item (passthrough). Use `toolbar` prop or `true` for default CE toolbar. */
export type SpreadsheetToolbarItem = {
  type?: string;
  content?: string;
  tooltip?: string;
  width?: string | number;
  options?: unknown[];
  data?: unknown[];
  columns?: number;
  right?: boolean;
  onclick?: (...args: unknown[]) => void;
  onchange?: (...args: unknown[]) => void;
  render?: (...args: unknown[]) => string;
  updateState?: (...args: unknown[]) => void;
  [key: string]: unknown;
};

export type SpreadsheetToolbarConfig =
  | boolean
  | SpreadsheetToolbarItem[]
  | { items: SpreadsheetToolbarItem[]; [key: string]: unknown };

/** One footer row — cell labels / formulas aligned to columns (+ row gutter is handled by CE). */
export type SpreadsheetFooterRow = string[];

export type SpreadsheetFooters =
  | SpreadsheetFooterRow[]
  | ((ctx: { data: unknown[]; columnKeys: string[] }) => SpreadsheetFooterRow[]);

export interface SpreadsheetChromeApi<T = unknown> {
  getWorksheet: () => SpreadsheetWorksheetInstance | null;
  data: T[];
  disabled: boolean;
  /** Download filename stem (no extension). */
  downloadFileName?: string;
  openFilter: (columnKeyOrIndex: string | number) => void;
  resetFilters: () => void;
  refresh: () => void;
  undo: () => void;
  redo: () => void;
  cut: () => void;
  copy: () => void;
  paste: () => void;
  search: (query: string) => void;
  clearSearch: () => void;
  /** Sort active/selected column. direction 0 = A→Z, 1 = Z→A. */
  sortActiveColumn: (direction: 0 | 1) => void;
  /** Apply CSS to the current selection (session-only). */
  applyStyle: (property: string, value: string) => void;
  /** Download visible rows as styled Excel (.xlsx) matching on-screen formatting. */
  downloadCsv: () => void;
}

export interface SpreadsheetColumnDef<T> {
  /** Unique field key used for object-mode sheet data and change mapping. */
  key: string;
  title: string;
  type?: SpreadsheetColumnType;
  width?: number | string;
  align?: SpreadsheetAlign;
  /**
   * After content-fit: when false, this column stays at content width and does not
   * absorb leftover sheet space. Default true (share free width so the sheet fills).
   */
  grow?: boolean;
  /**
   * When sheet `filters` is on, show a filter control for this column.
   * Omit to use the sheet default (all filterable when `filters` is true).
   */
  filter?: boolean;
  /**
   * API / server filter query key when different from `key` (e.g. companies → companyName).
   */
  filterKey?: string;
  /** Column-level read-only. Prefer over per-row when all rows share the same lock. */
  readOnly?: boolean;
  /**
   * Custom cell UI (picker, etc.): selectable like an editable cell (chrome + arrow keys),
   * but the native CE text editor is suppressed. Use `render` + click / Enter (`onActivate`).
   */
  customInteract?: boolean;
  /**
   * Called when the user activates a `customInteract` cell (Enter / F2 / edit gesture).
   * Click handlers inside `render` still work independently.
   */
  onActivate?: (row: T, rowIndex: number, cell: HTMLTableCellElement) => void;
  wordWrap?: boolean;
  mask?: string;
  decimal?: string;
  source?: SpreadsheetDropdownSourceItem[];
  autocomplete?: boolean;
  multiple?: boolean;
  /** Extract the sheet cell value from the row model. Defaults to `(row as any)[key]`. */
  getValue?: (row: T, rowIndex: number) => SpreadsheetCellValue;
  /** Apply an edited cell value back onto a row clone. Required for editable columns. */
  setValue?: (row: T, value: SpreadsheetCellValue, rowIndex: number) => T;
  /** Per-row read-only. Checked on edit attempts. */
  isReadOnly?: (row: T, rowIndex: number) => boolean;
  /** Custom DOM render after the cell value is applied. */
  render?: (
    cell: HTMLTableCellElement,
    value: SpreadsheetCellValue,
    row: T,
    rowIndex: number,
    instance: SpreadsheetWorksheetInstance
  ) => void;
  /** Extra jspreadsheet column options (passthrough). */
  jss?: Record<string, unknown>;
}

export interface SpreadsheetCellChange<T> {
  rowIndex: number;
  columnKey: string;
  columnIndex: number;
  value: SpreadsheetCellValue;
  oldValue: SpreadsheetCellValue;
  row: T;
}

export interface SpreadsheetGridHandle {
  getWorksheet: () => SpreadsheetWorksheetInstance | null;
  getData: () => Record<string, SpreadsheetCellValue>[];
  refresh: () => void;
  openFilter: (columnKeyOrIndex: string | number) => void;
  resetFilters: () => void;
}

export interface SpreadsheetGridProps<T> {
  columns: SpreadsheetColumnDef<T>[];
  data: T[];
  className?: string;
  /** Full-grid lock (covers editing, insert/delete). */
  disabled?: boolean;
  /** Shown when `data` is empty. Grid mounts only when there is at least one row unless `showWhenEmpty`. */
  emptyMessage?: string;
  /** Keep the grid mounted with min rows even when data is empty. */
  showWhenEmpty?: boolean;
  minDimensions?: [number, number];
  tableOverflow?: boolean;
  tableWidth?: string | number;
  tableHeight?: string | number;
  allowInsertRow?: boolean;
  allowDeleteRow?: boolean;
  allowInsertColumn?: boolean;
  allowDeleteColumn?: boolean;
  allowManualInsertRow?: boolean;
  allowManualInsertColumn?: boolean;
  columnSorting?: boolean;
  columnDrag?: boolean;
  rowDrag?: boolean;
  columnResize?: boolean;
  rowResize?: boolean;
  search?: boolean;
  pagination?: number;
  selectionCopy?: boolean;
  defaultColAlign?: SpreadsheetAlign;
  wordWrap?: boolean;
  /**
   * Enable CE column filters (header filter UI). Pair with `column.filter` to limit which columns filter.
   * @example <SpreadsheetGrid filters columns={[..., { key: 'name', filter: true }]} />
   */
  filters?: boolean;
  /**
   * T1.52 — header funnel uses debounced Contains text; parent refetches via `onColumnFilterChange`.
   * When true, Excel checklist DOM filtering is disabled.
   */
  serverFilters?: boolean;
  /** Current server filter values keyed by column `filterKey` or `key`. */
  columnFilters?: Record<string, string>;
  /** Fired when a server filter value changes (debounced ~300ms from the funnel). */
  onColumnFilterChange?: (key: string, value: string) => void;
  /**
   * Native jspreadsheet toolbar. `true` = CE default toolbar; or pass items / `{ items: [...] }`.
   * For React UI (buttons, Formik actions), prefer `renderToolbar` instead.
   */
  toolbar?: SpreadsheetToolbarConfig;
  /**
   * Native spreadsheet footer rows (labels / formulas). Recomputed when a function is passed and data changes
   * (triggers sheet remount for CE footers). For live React summaries, use `renderFooter`.
   */
  footers?: SpreadsheetFooters;
  /**
   * React chrome above the sheet (does not remount the worksheet).
   * @example renderToolbar={({ refresh, disabled }) => <button disabled={disabled} onClick={refresh}>Reload</button>}
   */
  renderToolbar?: (api: SpreadsheetChromeApi<T>) => ReactNode;
  /**
   * React chrome below the sheet (totals, legend, etc.). Prefer this over CE `footers` for app UI.
   * @example renderFooter={({ data }) => <div>Lines: {data.length}</div>}
   */
  renderFooter?: (api: SpreadsheetChromeApi<T>) => ReactNode;
  /** Excel download filename stem (no extension). Used by toolbar download. */
  downloadFileName?: string;
  /**
   * When true (default):
   * - arrow/click selection skips read-only and jumps directly to the nearest editable column
   * - column expand/collapse (resize) remains available on all columns
   */
  lockReadonlyInteraction?: boolean;
  /** Optional CSS class(es) for a row (applied to the first cell). */
  getRowClassName?: (row: T, rowIndex: number) => string | undefined;
  /**
   * Return a validation message for a cell to mark it invalid (red outline + title).
   * Called after every data sync — safe to derive from Formik errors/touched.
   */
  getCellError?: (row: T, rowIndex: number, columnKey: string) => string | undefined;
  /** Fires after editable cells change; receives the full updated row models. */
  onDataChange?: (nextData: T[]) => void;
  /** Granular cell-level change list plus the next data snapshot. */
  onCellChange?: (changes: SpreadsheetCellChange<T>[], nextData: T[]) => void;
  /**
   * Intercept row deletion. If provided, default sheet row-delete is cancelled
   * and this callback runs with the selected row indexes instead.
   */
  onDeleteRows?: (rowIndexes: number[]) => void;
  /**
   * Optional trailing actions column (HTML). Click handlers should use
   * `data-sg-action` + `data-sg-row` attributes; the grid delegates clicks.
   */
  actions?: {
    title?: string;
    width?: number | string;
    align?: SpreadsheetAlign;
    /** When false, keep preferred width (do not absorb leftover space). Default true. */
    grow?: boolean;
    renderHtml: (row: T, rowIndex: number) => string;
  };
  /** Named action click from `data-sg-action` buttons inside cells. */
  onActionClick?: (action: string, rowIndex: number, row: T) => void;
  /** Imperative handle (worksheet access). */
  gridRef?: RefObject<SpreadsheetGridHandle | null>;
  /** Extra worksheet options merged last (escape hatch). */
  worksheetOptions?: Record<string, unknown>;
  /** Extra spreadsheet-level options merged last (escape hatch). */
  spreadsheetOptions?: Record<string, unknown>;
}
