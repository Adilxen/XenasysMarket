'use client';

import {
  useCallback,
  useEffect,
  useId,
  useImperativeHandle,
  useMemo,
  useRef,
  type MutableRefObject,
} from 'react';
import { jspreadsheet } from '@jspreadsheet-ce/react';
import * as XLSX from 'xlsx-js-style';
import 'jspreadsheet-ce/dist/jspreadsheet.css';
import 'jsuites/dist/jsuites.css';
import './spreadsheetGrid.css';
import { activateSpreadsheetCustomInteract } from "@/components/ui/spreadsheet-grid/spreadsheetCustomInteract";
import { isSheetDatePickerOpen } from "@/components/ui/spreadsheet-grid/sheet-date-picker-stub";
import { formatHeadingTitle } from "@/lib/utils/to-title-case";
import type {
  SpreadsheetCellChange,
  SpreadsheetCellValue,
  SpreadsheetChromeApi,
  SpreadsheetColumnDef,
  SpreadsheetFooters,
  SpreadsheetGridHandle,
  SpreadsheetGridProps,
  SpreadsheetToolbarConfig,
  SpreadsheetWorksheetInstance,
} from "./types";

const ACTIONS_KEY = '__sg_actions';

type SheetCellChange = {
  value: SpreadsheetCellValue;
  oldValue: SpreadsheetCellValue;
  x: string | number;
  y: string | number;
};

function toCellValue(value: SpreadsheetCellValue): string | number | boolean {
  if (value === null || value === undefined) return '';
  return value;
}

function getNumericDecimalSep(decimal?: string): string {
  return decimal === ',' ? ',' : '.';
}

/**
 * Keep only a numeric draft: optional leading `-`, digits, at most one decimal separator.
 * Other characters (letters, symbols) are dropped.
 */
function sanitizeNumericDraft(raw: string, decimalSep = '.'): string {
  const s = String(raw ?? '');
  let out = '';
  let hasSep = false;
  let i = 0;
  if (s[i] === '-') {
    out = '-';
    i += 1;
  }
  for (; i < s.length; i++) {
    const ch = s[i];
    if (ch >= '0' && ch <= '9') {
      out += ch;
      continue;
    }
    if ((ch === '.' || ch === ',') && !hasSep) {
      out += decimalSep;
      hasSep = true;
    }
  }
  return out;
}

function isValidNumericDraft(value: string, decimalSep = '.'): boolean {
  if (value === '' || value === '-') return true;
  if (value === decimalSep || value === `-${decimalSep}`) return true;
  const escaped = decimalSep === '.' ? '\\.' : `,`;
  return new RegExp(`^-?\\d*${escaped}?\\d*$`).test(value);
}

/** Commit-time cleanup for numeric cells (empty / lone `-` / lone `.` → clear). */
function coerceNumericCommitValue(
  value: SpreadsheetCellValue,
  decimalSep: string
): SpreadsheetCellValue {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') return Number.isFinite(value) ? value : '';
  if (typeof value === 'boolean') return value ? 1 : 0;
  const raw = String(value).trim();
  if (raw === '') return '';
  const cleaned = sanitizeNumericDraft(raw, decimalSep);
  if (
    cleaned === '' ||
    cleaned === '-' ||
    cleaned === decimalSep ||
    cleaned === `-${decimalSep}`
  ) {
    return '';
  }
  return cleaned;
}

type SgNumericBoundEl = HTMLElement & { __sgNumericBound?: boolean };

/** Block non-numeric keystrokes / paste while a numeric cell editor is open. */
function bindNumericEditorInput(
  input: HTMLInputElement | HTMLTextAreaElement,
  decimalSep: string
): void {
  const el = input as SgNumericBoundEl;
  if (el.__sgNumericBound) return;
  el.__sgNumericBound = true;

  input.setAttribute('inputmode', 'decimal');
  input.setAttribute('autocomplete', 'off');

  const setSanitized = (next: string, preferredCaret?: number) => {
    const cleaned = sanitizeNumericDraft(next, decimalSep);
    input.value = cleaned;
    const caret = Math.min(
      preferredCaret ?? cleaned.length,
      cleaned.length
    );
    try {
      input.setSelectionRange(caret, caret);
    } catch {
      /* ignore — some input types reject setSelectionRange */
    }
  };

  input.addEventListener("keydown", (ev) => {
    const ke = ev as KeyboardEvent;
    if (ke.ctrlKey || ke.metaKey || ke.altKey) return;
    if (ke.key.length !== 1) return;
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? 0;
    const next = input.value.slice(0, start) + ke.key + input.value.slice(end);
    if (!isValidNumericDraft(next, decimalSep)) {
      ke.preventDefault();
    }
  });

  input.addEventListener("beforeinput", (ev) => {
    const e = ev as InputEvent;
    const type = e.inputType || "";
    if (
      type.startsWith("delete") ||
      type === "historyUndo" ||
      type === "historyRedo" ||
      type === "insertLineBreak" ||
      type === "insertParagraph"
    ) {
      return;
    }
    if (e.data == null) return;
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? 0;
    const insert =
      type === "insertFromPaste" || type === "insertFromDrop"
        ? sanitizeNumericDraft(e.data, decimalSep)
        : e.data;
    const next = input.value.slice(0, start) + insert + input.value.slice(end);
    if (isValidNumericDraft(next, decimalSep) && insert === e.data) return;
    e.preventDefault();
    const cleaned = sanitizeNumericDraft(next, decimalSep);
    if (cleaned !== input.value) {
      setSanitized(cleaned, start + insert.length);
    }
  });

  input.addEventListener("paste", (ev) => {
    const pe = ev as ClipboardEvent;
    const text = pe.clipboardData?.getData("text/plain") ?? "";
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? 0;
    const merged = input.value.slice(0, start) + text + input.value.slice(end);
    if (isValidNumericDraft(merged, decimalSep)) return;
    pe.preventDefault();
    const cleaned = sanitizeNumericDraft(merged, decimalSep);
    setSanitized(cleaned, start + sanitizeNumericDraft(text, decimalSep).length);
  });

  // Catch-all if anything slips past keydown/beforeinput (IME, drag-drop, etc.).
  input.addEventListener('input', () => {
    if (isValidNumericDraft(input.value, decimalSep)) return;
    const pos = input.selectionStart ?? input.value.length;
    setSanitized(input.value, Math.max(0, pos - 1));
  });
}

function findEditorTextControl(
  cell: HTMLTableCellElement,
  editor: unknown
): HTMLInputElement | HTMLTextAreaElement | null {
  if (editor instanceof HTMLInputElement || editor instanceof HTMLTextAreaElement) {
    return editor;
  }
  if (editor instanceof HTMLElement) {
    const nested = editor.querySelector('input, textarea');
    if (nested instanceof HTMLInputElement || nested instanceof HTMLTextAreaElement) {
      return nested;
    }
  }
  const child = cell.querySelector('input, textarea');
  if (child instanceof HTMLInputElement || child instanceof HTMLTextAreaElement) {
    return child;
  }
  return null;
}

/** CSS color → Excel RGB hex (no #). */
function cssColorToExcelRgb(input: string | undefined | null): string | null {
  if (!input) return null;
  const s = input.trim().toLowerCase();
  if (!s || s === 'transparent' || s === 'inherit' || s === 'initial') return null;
  if (s.startsWith('#')) {
    let hex = s.slice(1);
    if (hex.length === 3) {
      hex = hex
        .split('')
        .map((c) => c + c)
        .join('');
    }
    if (hex.length === 8) hex = hex.slice(0, 6);
    if (hex.length !== 6 || /[^0-9a-f]/i.test(hex)) return null;
    return hex.toUpperCase();
  }
  const m = s.match(/rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i);
  if (!m) return null;
  const aMatch = s.match(/rgba?\([^)]+,\s*([0-9.]+)\s*\)/i);
  if (aMatch && Number(aMatch[1]) === 0) return null;
  return [m[1], m[2], m[3]]
    .map((n) => Number(n).toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();
}

/** Map toolbar / inline CSS props to xlsx-js-style cell style. */
function cssPropsToExcelStyle(
  props: Record<string, string>
): Record<string, unknown> | undefined {
  const font: Record<string, unknown> = {};
  const fill: Record<string, unknown> = {};
  const alignment: Record<string, unknown> = {};

  const colorRgb = cssColorToExcelRgb(props.color);
  if (colorRgb) font.color = { rgb: colorRgb };

  const weight = (props['font-weight'] ?? '').toLowerCase();
  if (weight === 'bold' || weight === 'bolder' || Number(weight) >= 600) {
    font.bold = true;
  }

  const bgRgb = cssColorToExcelRgb(
    props['background-color'] ?? props.background
  );
  if (bgRgb) {
    fill.patternType = 'solid';
    fill.fgColor = { rgb: bgRgb };
  }

  const align = (props['text-align'] ?? '').toLowerCase();
  if (align === 'left' || align === 'center' || align === 'right' || align === 'justify') {
    alignment.horizontal = align;
  }

  const style: Record<string, unknown> = {};
  if (Object.keys(font).length) style.font = font;
  if (Object.keys(fill).length) style.fill = fill;
  if (Object.keys(alignment).length) style.alignment = alignment;
  return Object.keys(style).length ? style : undefined;
}

function readDomCellCssProps(cell: HTMLElement): Record<string, string> {
  const props: Record<string, string> = {};
  for (const key of ['color', 'background-color', 'font-weight', 'text-align'] as const) {
    const v = cell.style.getPropertyValue(key)?.trim();
    if (v) props[key] = v;
  }
  return props;
}

/** Normalize title for width math and header display (`\n` → space). */
function flattenTitle(title: string | undefined): string {
  return String(title ?? '')
    .replace(/\r\n|\r|\n/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Bold Title Case header label (preserves FOC/PR/QR-style acronyms). */
function formatColumnHeaderTitle(title: string | undefined): string {
  const flat = flattenTitle(title);
  if (!flat || flat === '\u00A0') return flat;
  return formatHeadingTitle(flat);
}

function resolveFooters<T>(
  footers: SpreadsheetFooters | undefined,
  data: T[],
  columns: SpreadsheetColumnDef<T>[]
): string[][] | undefined {
  if (footers == null) return undefined;
  if (typeof footers === 'function') {
    return footers({
      data,
      columnKeys: columns.map((c) => c.key),
    });
  }
  return footers;
}

/**
 * CE filters index `options.data[row][colIndex]`. Object-mode rows break that —
 * coerce to a parallel matrix (labels stay on the sheet cells either way).
 */
function coerceWorksheetDataMatrixForFilters(worksheet: SpreadsheetWorksheetInstance) {
  const options = worksheet.options;
  if (!options?.filters) return;
  const data = options.data as unknown;
  const cols = options.columns as { name?: string }[] | undefined;
  if (!Array.isArray(data) || data.length === 0 || !cols?.length) return;
  if (Array.isArray(data[0])) return;

  options.data = (data as Record<string, unknown>[]).map((row) =>
    cols.map((c, i) => {
      if (c?.name != null && row != null && typeof row === 'object') {
        return (row as Record<string, unknown>)[c.name] ?? '';
      }
      return Array.isArray(row) ? (row as unknown[])[i] ?? '' : '';
    })
  );
}

const FILTER_FUNNEL_SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z"/><path d="M0 0h24v24H0z" fill="none"/></svg>';

const SORT_IDLE_SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M7 10l5-5 5 5H7zm0 4l5 5 5-5H7z"/></svg>';
const SORT_ASC_SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M7 14l5-5 5 5H7z"/></svg>';
const SORT_DESC_SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M7 10l5 5 5-5H7z"/></svg>';

/** Per-worksheet column → allowed display values (AND across columns). */
const sgColumnFilterState = new WeakMap<object, Map<number, Set<string>>>();

/** Per-worksheet active sort: column index + direction (0=asc, 1=desc). */
const sgColumnSortState = new WeakMap<object, { col: number; dir: 0 | 1 }>();

/** T1.52 — server Contains filters (parent owns data; no DOM row hide). */
type SgServerFilterBridge = {
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
  keyByCol: (colIndex: number) => string | null;
};

const sgServerFilterBridge = new WeakMap<object, SgServerFilterBridge>();

/** Authoritative visible row count after last pushSheetData (CE may pad tbody). */
const sgSheetRowCount = new WeakMap<object, number>();

function getSgColumnFilterState(worksheet: object): Map<number, Set<string>> {
  let map = sgColumnFilterState.get(worksheet);
  if (!map) {
    map = new Map();
    sgColumnFilterState.set(worksheet, map);
  }
  return map;
}

function paintSgServerFilterIndicators(worksheet: SpreadsheetWorksheetInstance) {
  const table = worksheet.table;
  const bridge = sgServerFilterBridge.get(worksheet);
  if (!table || !bridge) return;
  table.querySelectorAll('.sg-excel-filter').forEach((el) => {
    const btn = el as HTMLElement;
    const col = Number(btn.dataset.col);
    const key = bridge.keyByCol(col);
    const active = Boolean(key && (bridge.values[key] ?? '').trim().length > 0);
    btn.classList.toggle('sg-excel-filter-active', active);
  });
}

function paintSgColumnSortIndicators(worksheet: SpreadsheetWorksheetInstance) {
  const table = worksheet.table;
  if (!table) return;
  const state = sgColumnSortState.get(worksheet);
  table.querySelectorAll('.sg-excel-sort').forEach((el) => {
    const btn = el as HTMLElement;
    const col = Number(btn.dataset.col);
    const active = Boolean(state && state.col === col);
    const dir = active && state ? state.dir : null;
    btn.classList.toggle('sg-excel-sort-active', active);
    btn.dataset.dir = dir == null ? '' : String(dir);
    if (dir === 0) {
      btn.title = 'Sorted ascending — click for descending';
      btn.setAttribute('aria-label', 'Sorted ascending');
      btn.innerHTML = SORT_ASC_SVG;
    } else if (dir === 1) {
      btn.title = 'Sorted descending — click for ascending';
      btn.setAttribute('aria-label', 'Sorted descending');
      btn.innerHTML = SORT_DESC_SVG;
    } else {
      btn.title = 'Sort ascending';
      btn.setAttribute('aria-label', 'Sort column');
      btn.innerHTML = SORT_IDLE_SVG;
    }
  });
}

function closeSgColumnMenus() {
  document.querySelectorAll('.sg-col-menu').forEach((el) => el.remove());
  document.removeEventListener('mousedown', onSgColumnMenuOutside, true);
  document.removeEventListener('keydown', onSgColumnMenuEscape, true);
}

function onSgColumnMenuOutside(event: MouseEvent) {
  const t = event.target as HTMLElement | null;
  if (
    t?.closest?.('.sg-col-menu') ||
    t?.closest?.('.sg-excel-filter') ||
    t?.closest?.('.sg-excel-sort')
  ) {
    return;
  }
  closeSgColumnMenus();
}

function onSgColumnMenuEscape(event: KeyboardEvent) {
  if (event.key !== 'Escape') return;
  const active = document.activeElement;
  if (active instanceof HTMLInputElement && active.classList.contains('sg-col-menu-search')) {
    if (active.value) {
      event.preventDefault();
      active.value = '';
      active.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    }
  }
  closeSgColumnMenus();
}

/** Strip residual markup when a stored cell value is HTML (CE native copy uses innerHTML). */
function stripHtmlToPlainText(raw: string): string {
  if (!raw || !/<[^>]+>/.test(raw)) return raw;
  const el = document.createElement('div');
  el.innerHTML = raw;
  return (el.innerText ?? el.textContent ?? '')
    .replace(/\u00a0/g, ' ')
    .replace(/\r\n|\r/g, '\n');
}

function escapeTsvCell(raw: string): string {
  if (/[\t\n\r"]/.test(raw)) return `"${raw.replace(/"/g, '""')}"`;
  return raw;
}

/** Visible cell text for clipboard — never HTML tags. */
function readWorksheetCellPlainText(
  worksheet: SpreadsheetWorksheetInstance,
  x: number,
  y: number
): string {
  try {
    const cell =
      worksheet.getCellFromCoords?.(x, y) ?? worksheet.getCell?.(x, y) ?? null;
    if (cell && cell.style.display === 'none') return '';
    let text = (cell?.innerText ?? cell?.textContent ?? '')
      .replace(/\u00a0/g, ' ')
      .replace(/\r\n|\r/g, '\n')
      .trimEnd();
    if (!text) {
      const v = worksheet.getValue?.(x, y);
      text = v == null ? '' : String(v);
    }
    return stripHtmlToPlainText(text).trimEnd();
  } catch {
    const v = worksheet.getValue?.(x, y);
    return stripHtmlToPlainText(v == null ? '' : String(v)).trimEnd();
  }
}

/**
 * Build tab-separated plain text for a selection (same shape Excel/Sheets paste expects).
 * Skips rows hidden via `display:none` (e.g. filtered-out).
 */
function buildSelectionPlainTsv(
  worksheet: SpreadsheetWorksheetInstance,
  x1: number,
  y1: number,
  x2: number,
  y2: number
): string {
  const xa = Math.min(x1, x2);
  const xb = Math.max(x1, x2);
  const ya = Math.min(y1, y2);
  const yb = Math.max(y1, y2);
  const lines: string[] = [];

  for (let y = ya; y <= yb; y++) {
    const rowEl = (
      worksheet as SpreadsheetWorksheetInstance & {
        rows?: Array<{ element?: HTMLElement }>;
      }
    ).rows?.[y]?.element;
    if (rowEl && rowEl.style.display === 'none') continue;

    const cells: string[] = [];
    for (let x = xa; x <= xb; x++) {
      cells.push(escapeTsvCell(readWorksheetCellPlainText(worksheet, x, y)));
    }
    lines.push(cells.join('\t'));
  }

  return lines.join('\r\n');
}

/**
 * Stable filter text from sheet data (not DOM).
 * React portal cells often have empty/late textContent — filtering by DOM
 * leaves blank rows visible or hides real matches.
 */
function formatSgFilterDisplayValue(raw: unknown): string {
  if (raw === undefined || raw === null) return '';
  if (typeof raw === 'boolean') return raw ? 'Active' : 'Inactive';
  if (typeof raw === 'number') return String(raw);
  if (typeof raw === 'string') {
    const trimmed = raw.replace(/\s+/g, ' ').trim();
    if (trimmed === 'true') return 'Active';
    if (trimmed === 'false') return 'Inactive';
    // Actions / HTML cells — treat markup as blank for value filters.
    if (/<[a-z][\s\S]*>/i.test(trimmed)) return '';
    return trimmed;
  }
  return String(raw).replace(/\s+/g, ' ').trim();
}

function readSgFilterCellValue(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number,
  rowIndex: number
): string {
  const data = worksheet.options?.data as unknown;
  const cols = worksheet.options?.columns as { name?: string }[] | undefined;
  if (Array.isArray(data) && rowIndex >= 0 && rowIndex < data.length) {
    const row = data[rowIndex] as unknown;
    if (Array.isArray(row)) {
      return formatSgFilterDisplayValue(row[colIndex]);
    }
    if (row && typeof row === 'object') {
      const name = cols?.[colIndex]?.name;
      if (name) {
        return formatSgFilterDisplayValue((row as Record<string, unknown>)[name]);
      }
    }
  }

  try {
    const v = worksheet.getValue?.(colIndex, rowIndex);
    if (v != null && v !== '') return formatSgFilterDisplayValue(v);
  } catch {
    /* ignore */
  }

  const cell =
    worksheet.getCellFromCoords?.(colIndex, rowIndex) ??
    worksheet.getCell?.(colIndex, rowIndex);
  return formatSgFilterDisplayValue(cell?.textContent ?? '');
}

function collectColumnDisplayValues(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number
): string[] {
  const values = new Set<string>();
  const rowCount =
    (Array.isArray(worksheet.options?.data) ? worksheet.options!.data!.length : 0) ||
    (worksheet as { rows?: unknown[] }).rows?.length ||
    0;

  for (let y = 0; y < rowCount; y++) {
    values.add(readSgFilterCellValue(worksheet, colIndex, y));
  }
  return Array.from(values).sort((a, b) =>
    a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
  );
}

function isSgBlankDataRow(
  worksheet: SpreadsheetWorksheetInstance,
  rowIndex: number
): boolean {
  const cols = (worksheet.options?.columns as { name?: string }[] | undefined) ?? [];
  for (let x = 0; x < cols.length; x++) {
    const name = cols[x]?.name;
    if (name === ACTIONS_KEY) continue;
    if (readSgFilterCellValue(worksheet, x, rowIndex)) return false;
  }
  return true;
}

function reapplySgColumnFilters(worksheet: SpreadsheetWorksheetInstance) {
  const table = worksheet.table;
  if (!table) return;

  const serverMode = sgServerFilterBridge.has(worksheet);
  if (serverMode) {
    paintSgServerFilterIndicators(worksheet);
  }

  const state = getSgColumnFilterState(worksheet);
  const bodyRows = table.querySelectorAll('tbody > tr');
  const dataLen =
    sgSheetRowCount.get(worksheet) ??
    (Array.isArray(worksheet.options?.data) ? worksheet.options!.data!.length : 0);

  bodyRows.forEach((tr) => {
    const y = Number(tr.getAttribute('data-y'));
    // CE padding / orphan rows after a shrink (e.g. server filter) must stay hidden.
    if (!Number.isFinite(y) || y < 0 || y >= dataLen || isSgBlankDataRow(worksheet, y)) {
      tr.classList.add('sg-row-filtered-out');
      return;
    }
    if (serverMode) {
      // Parent already filtered via API — show every real data row.
      tr.classList.remove('sg-row-filtered-out');
      return;
    }
    let visible = true;
    for (const [col, allowed] of state) {
      const text = readSgFilterCellValue(worksheet, col, y);
      if (!allowed.has(text)) {
        visible = false;
        break;
      }
    }
    tr.classList.toggle('sg-row-filtered-out', !visible);
  });

  if (serverMode) return;

  // Mark active filter buttons (client checklist mode only).
  table.querySelectorAll('.sg-excel-filter').forEach((btn) => {
    const col = Number((btn as HTMLElement).dataset.col);
    (btn as HTMLElement).classList.toggle('sg-excel-filter-active', state.has(col));
  });
}

/**
 * CE `search()` indexes `row.length` — object-mode sheet data never matches.
 * Filter visible rows by displayed cell text instead (toolbar Find).
 */
function applySgSheetSearch(worksheet: SpreadsheetWorksheetInstance, query: string) {
  const table = worksheet.table;
  if (!table) return;
  const q = query.trim().toLowerCase();
  const bodyRows = table.querySelectorAll('tbody > tr');

  bodyRows.forEach((tr) => {
    if (!q) {
      tr.classList.remove('sg-row-search-hidden');
      return;
    }
    const text = (tr.textContent ?? '').replace(/\s+/g, ' ').toLowerCase();
    tr.classList.toggle('sg-row-search-hidden', !text.includes(q));
  });
}

function sortWorksheetColumn(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number,
  direction: 0 | 1
) {
  // Prefer DOM reorder — CE orderBy reshuffles data indices and wipes React portals,
  // which surfaces as empty filtered/sorted rows in DataTable listings.
  const table = worksheet.table;
  const tbody = table?.querySelector('tbody');
  if (!tbody) return;
  const rows = Array.from(tbody.querySelectorAll(':scope > tr'));
  rows.sort((a, b) => {
    const ya = Number(a.getAttribute('data-y'));
    const yb = Number(b.getAttribute('data-y'));
    const ta = Number.isFinite(ya) ? readSgFilterCellValue(worksheet, colIndex, ya) : '';
    const tb = Number.isFinite(yb) ? readSgFilterCellValue(worksheet, colIndex, yb) : '';
    const cmp = ta.localeCompare(tb, undefined, { numeric: true, sensitivity: 'base' });
    return direction === 0 ? cmp : -cmp;
  });
  rows.forEach((r) => tbody.appendChild(r));
  sgColumnSortState.set(worksheet, { col: colIndex, dir: direction });
  paintSgColumnSortIndicators(worksheet);
  reapplySgColumnFilters(worksheet);
}

function toggleWorksheetColumnSort(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number
) {
  const current = sgColumnSortState.get(worksheet);
  const nextDir: 0 | 1 =
    current && current.col === colIndex ? ((current.dir === 0 ? 1 : 0) as 0 | 1) : 0;
  sortWorksheetColumn(worksheet, colIndex, nextDir);
}

/**
 * T1.52 — funnel menu: sort + debounced Contains text (server refetch).
 */
function openSgServerContainsMenu(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number,
  anchorEl: HTMLElement,
  bridge: SgServerFilterBridge
) {
  const filterKey = bridge.keyByCol(colIndex);
  if (!filterKey) return;

  closeSgColumnMenus();

  const menu = document.createElement('div');
  menu.className = 'sg-col-menu';
  menu.setAttribute('role', 'menu');

  const br = anchorEl.getBoundingClientRect();
  menu.style.left = `${Math.round(br.left)}px`;
  menu.style.top = `${Math.round(br.bottom + 4)}px`;
  menu.style.minWidth = `${Math.max(240, Math.round(br.width))}px`;

  const initial = bridge.values[filterKey] ?? '';
  menu.innerHTML = `
    <button type="button" class="sg-col-menu-item" data-action="sort-asc" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">A→Z</span>
      <span>Sort A to Z</span>
    </button>
    <button type="button" class="sg-col-menu-item" data-action="sort-desc" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">Z→A</span>
      <span>Sort Z to A</span>
    </button>
    <div class="sg-col-menu-sep" role="separator"></div>
    <div class="sg-col-menu-heading">Filter (contains)</div>
    <div class="sg-col-menu-search-wrap">
      <input
        type="search"
        class="sg-col-menu-search"
        data-role="contains"
        placeholder="Type to filter…"
        autocomplete="off"
        spellcheck="false"
      />
    </div>
    <div class="sg-col-menu-sep" role="separator"></div>
    <button type="button" class="sg-col-menu-item" data-action="clear" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">✕</span>
      <span>Clear filter</span>
    </button>
  `;

  const searchInput = menu.querySelector(
    '[data-role="contains"]'
  ) as HTMLInputElement;
  searchInput.value = initial;

  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  const emit = (raw: string) => {
    bridge.onChange(filterKey, raw);
    // Keep bridge.values in sync for active funnel paint before parent re-renders.
    bridge.values[filterKey] = raw;
    paintSgServerFilterIndicators(worksheet);
  };

  searchInput.addEventListener('input', () => {
    if (debounceTimer != null) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      debounceTimer = null;
      emit(searchInput.value);
    }, 300);
  });

  searchInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      if (debounceTimer != null) clearTimeout(debounceTimer);
      emit(searchInput.value);
      closeSgColumnMenus();
    }
  });

  menu.addEventListener('click', (event) => {
    const target = event.target as HTMLElement | null;
    const action = target?.closest?.('[data-action]')?.getAttribute('data-action');
    if (!action) return;
    event.preventDefault();
    event.stopPropagation();
    if (action === 'sort-asc') {
      sortWorksheetColumn(worksheet, colIndex, 0);
      closeSgColumnMenus();
      return;
    }
    if (action === 'sort-desc') {
      sortWorksheetColumn(worksheet, colIndex, 1);
      closeSgColumnMenus();
      return;
    }
    if (action === 'clear') {
      if (debounceTimer != null) clearTimeout(debounceTimer);
      searchInput.value = '';
      emit('');
      closeSgColumnMenus();
    }
  });

  document.body.appendChild(menu);
  requestAnimationFrame(() => {
    const rect = menu.getBoundingClientRect();
    if (rect.right > window.innerWidth - 8) {
      menu.style.left = `${Math.max(8, window.innerWidth - rect.width - 8)}px`;
    }
    if (rect.bottom > window.innerHeight - 8) {
      menu.style.top = `${Math.max(8, br.top - rect.height - 4)}px`;
    }
  });

  searchInput.focus();
  searchInput.select();

  document.addEventListener('mousedown', onSgColumnMenuOutside, true);
  document.addEventListener('keydown', onSgColumnMenuEscape, true);
}

/**
 * Google Sheets-style column menu (sort + filter-by-value). Portaled to body (no clip).
 * When a server-filter bridge is set (T1.52), opens Contains text menu instead.
 */
function openSgColumnFilterMenu(
  worksheet: SpreadsheetWorksheetInstance,
  colIndex: number,
  anchorEl: HTMLElement
) {
  const serverBridge = sgServerFilterBridge.get(worksheet);
  if (serverBridge) {
    openSgServerContainsMenu(worksheet, colIndex, anchorEl, serverBridge);
    return;
  }

  closeSgColumnMenus();
  coerceWorksheetDataMatrixForFilters(worksheet);

  const allValues = collectColumnDisplayValues(worksheet, colIndex);
  const state = getSgColumnFilterState(worksheet);
  const active = state.get(colIndex);
  const selected = new Set(active ?? allValues);

  const menu = document.createElement('div');
  menu.className = 'sg-col-menu';
  menu.setAttribute('role', 'menu');

  const br = anchorEl.getBoundingClientRect();
  menu.style.left = `${Math.round(br.left)}px`;
  menu.style.top = `${Math.round(br.bottom + 4)}px`;
  menu.style.minWidth = `${Math.max(240, Math.round(br.width))}px`;

  menu.innerHTML = `
    <button type="button" class="sg-col-menu-item" data-action="sort-asc" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">A→Z</span>
      <span>Sort A to Z</span>
    </button>
    <button type="button" class="sg-col-menu-item" data-action="sort-desc" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">Z→A</span>
      <span>Sort Z to A</span>
    </button>
    <div class="sg-col-menu-sep" role="separator"></div>
    <div class="sg-col-menu-heading">Filter by values</div>
    <div class="sg-col-menu-search-wrap">
      <input
        type="search"
        class="sg-col-menu-search"
        data-role="search"
        placeholder="Search values…"
        autocomplete="off"
        spellcheck="false"
      />
    </div>
    <label class="sg-col-menu-check sg-col-menu-check-all">
      <input type="checkbox" data-role="select-all" />
      <span>Select all</span>
    </label>
    <div class="sg-col-menu-values" data-role="values"></div>
    <div class="sg-col-menu-sep" role="separator"></div>
    <button type="button" class="sg-col-menu-item" data-action="clear" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">✕</span>
      <span>Clear filter</span>
    </button>
    <button type="button" class="sg-col-menu-item sg-col-menu-item-primary" data-action="apply" role="menuitem">
      <span class="sg-col-menu-ico" aria-hidden="true">✓</span>
      <span>Apply filter</span>
    </button>
  `;

  const valuesHost = menu.querySelector('[data-role="values"]') as HTMLElement;
  const selectAll = menu.querySelector(
    '[data-role="select-all"]'
  ) as HTMLInputElement;
  const searchInput = menu.querySelector(
    '[data-role="search"]'
  ) as HTMLInputElement;

  const visibleValueLabels = () =>
    Array.from(valuesHost.querySelectorAll<HTMLLabelElement>(':scope > .sg-col-menu-check')).filter(
      (label) => !label.hidden
    );

  const syncSelectAll = () => {
    const visible = visibleValueLabels();
    const visibleChecked = visible.filter(
      (label) => (label.querySelector('input[type="checkbox"]') as HTMLInputElement | null)?.checked
    );
    selectAll.checked = visible.length > 0 && visibleChecked.length === visible.length;
    selectAll.indeterminate =
      visibleChecked.length > 0 && visibleChecked.length < visible.length;
  };

  const applySearchFilter = () => {
    const q = searchInput.value.trim().toLowerCase();
    valuesHost.querySelectorAll<HTMLLabelElement>(':scope > .sg-col-menu-check').forEach((label) => {
      const value = label.dataset.value ?? '';
      const display = value === '' ? '(blanks)' : value;
      label.hidden = Boolean(q) && !display.toLowerCase().includes(q);
    });
    const empty = menu.querySelector('[data-role="search-empty"]') as HTMLElement | null;
    const anyVisible = visibleValueLabels().length > 0;
    if (!anyVisible && q) {
      if (!empty) {
        const msg = document.createElement('div');
        msg.className = 'sg-col-menu-search-empty';
        msg.dataset.role = 'search-empty';
        msg.textContent = 'No matching values';
        valuesHost.appendChild(msg);
      }
    } else {
      empty?.remove();
    }
    syncSelectAll();
  };

  for (const value of allValues) {
    const label = document.createElement('label');
    label.className = 'sg-col-menu-check';
    label.dataset.value = value;
    const input = document.createElement('input');
    input.type = 'checkbox';
    input.checked = selected.has(value);
    input.addEventListener('change', () => {
      if (input.checked) selected.add(value);
      else selected.delete(value);
      syncSelectAll();
    });
    const text = document.createElement('span');
    text.textContent = value === '' ? '(Blanks)' : value;
    text.title = value === '' ? '(Blanks)' : value;
    label.appendChild(input);
    label.appendChild(text);
    valuesHost.appendChild(label);
  }
  syncSelectAll();

  searchInput.addEventListener('input', applySearchFilter);
  searchInput.addEventListener('keydown', (event) => {
    // Keep typing in the search box from bubbling to the sheet/CE.
    event.stopPropagation();
  });

  selectAll.addEventListener('change', () => {
    const visible = visibleValueLabels();
    for (const label of visible) {
      const value = label.dataset.value ?? '';
      const input = label.querySelector('input[type="checkbox"]') as HTMLInputElement | null;
      if (!input) continue;
      input.checked = selectAll.checked;
      if (selectAll.checked) selected.add(value);
      else selected.delete(value);
    }
    syncSelectAll();
  });

  menu.addEventListener('click', (event) => {
    const btn = (event.target as HTMLElement | null)?.closest?.(
      '[data-action]'
    ) as HTMLElement | null;
    if (!btn) return;
    const action = btn.getAttribute('data-action');
    if (action === 'sort-asc') {
      sortWorksheetColumn(worksheet, colIndex, 0);
      closeSgColumnMenus();
      return;
    }
    if (action === 'sort-desc') {
      sortWorksheetColumn(worksheet, colIndex, 1);
      closeSgColumnMenus();
      return;
    }
    if (action === 'clear') {
      state.delete(colIndex);
      reapplySgColumnFilters(worksheet);
      closeSgColumnMenus();
      return;
    }
    if (action === 'apply') {
      if (selected.size === 0 || selected.size === allValues.length) {
        state.delete(colIndex);
      } else {
        state.set(colIndex, new Set(selected));
      }
      reapplySgColumnFilters(worksheet);
      closeSgColumnMenus();
    }
  });

  document.body.appendChild(menu);
  queueMicrotask(() => searchInput.focus());

  // Keep on-screen if near viewport edge.
  requestAnimationFrame(() => {
    const rect = menu.getBoundingClientRect();
    if (rect.right > window.innerWidth - 8) {
      menu.style.left = `${Math.max(8, window.innerWidth - rect.width - 8)}px`;
    }
    if (rect.bottom > window.innerHeight - 8) {
      menu.style.top = `${Math.max(8, br.top - rect.height - 4)}px`;
    }
  });

  document.addEventListener('mousedown', onSgColumnMenuOutside, true);
  document.addEventListener('keydown', onSgColumnMenuEscape, true);
}

/**
 * Excel/Sheets-style: funnel next to column name opens our portaled menu.
 * CE's native filter row is removed — it only left an empty blank strip.
 */
function wireExcelStyleHeaderFilters(worksheet: SpreadsheetWorksheetInstance) {
  if (!worksheet.options?.filters) return;

  const table =
    (worksheet.table as HTMLTableElement | undefined) ??
    (worksheet.element as HTMLElement | undefined)?.querySelector?.('table') ??
    null;
  if (!table) return;

  const titleRow = table.querySelector(
    'thead tr:not(.jss_filter)'
  ) as HTMLTableRowElement | null;
  const filterRow =
    ((worksheet as unknown as { filter?: HTMLTableRowElement }).filter as
      | HTMLTableRowElement
      | undefined) ??
    (table.querySelector('thead tr.jss_filter') as HTMLTableRowElement | null);
  if (!titleRow) return;

  // Drop CE filter row entirely — we use header funnels + custom menu only.
  if (filterRow?.parentNode) {
    filterRow.remove();
  }
  // Clear CE's pointer so it doesn't resurrect / reference a detached row.
  try {
    (worksheet as unknown as { filter?: HTMLTableRowElement | null }).filter = null;
  } catch {
    /* ignore */
  }

  table.classList.add('sg-has-excel-filters');

  const titleCells = titleRow.querySelectorAll(':scope > td');
  const colCount = Math.max(
    titleCells.length - 1,
    ((worksheet.options.columns as unknown[]) ?? []).length
  );

  for (let i = 1; i <= colCount; i++) {
    const titleTd = titleCells[i] as HTMLTableCellElement | undefined;
    if (!titleTd) continue;

    const colIndex = i - 1;
    const colDef = (worksheet.options.columns as { filter?: boolean }[] | undefined)?.[
      colIndex
    ];
    if (colDef && colDef.filter === false) continue;

    titleTd.classList.add('sg-header-with-filter');

    if (!titleTd.querySelector('.sg-excel-sort')) {
      const sortBtn = document.createElement('button');
      sortBtn.type = 'button';
      sortBtn.className = 'sg-excel-sort';
      sortBtn.dataset.col = String(colIndex);
      sortBtn.title = 'Sort ascending';
      sortBtn.setAttribute('aria-label', 'Sort column');
      sortBtn.innerHTML = SORT_IDLE_SVG;
      sortBtn.addEventListener('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        toggleWorksheetColumnSort(worksheet, colIndex);
      });
      titleTd.appendChild(sortBtn);
    }

    if (!titleTd.querySelector('.sg-excel-filter')) {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'sg-excel-filter';
      btn.dataset.col = String(colIndex);
      btn.title = 'Filter';
      btn.setAttribute('aria-label', 'Filter column');
      btn.innerHTML = FILTER_FUNNEL_SVG;

      btn.addEventListener('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        openSgColumnFilterMenu(worksheet, colIndex, titleTd);
      });

      titleTd.appendChild(btn);
    }
  }

  paintSgColumnSortIndicators(worksheet);
  reapplySgColumnFilters(worksheet);
}

/**
 * Apply / refresh CE worksheet footers without remounting the grid.
 * Plain text cells (no formula engine) — ideal for computed money totals.
 */
function applyWorksheetFooters(
  worksheet: SpreadsheetWorksheetInstance,
  footers: string[][] | undefined
) {
  const table = worksheet.table;
  if (!table) return;

  if (!worksheet.options) worksheet.options = {};
  worksheet.options.footers = footers;

  if (!footers || footers.length === 0) {
    table.querySelector('tfoot')?.remove();
    return;
  }

  let tfoot = table.querySelector('tfoot') as HTMLTableSectionElement | null;
  if (!tfoot) {
    tfoot = document.createElement('tfoot');
    table.appendChild(tfoot);
  }

  const headersLen = Array.isArray(
    (worksheet as unknown as { headers?: unknown[] }).headers
  )
    ? ((worksheet as unknown as { headers: unknown[] }).headers.length)
    : footers.reduce((max, row) => Math.max(max, row.length), 0);

  while (tfoot.children.length > footers.length) {
    tfoot.lastElementChild?.remove();
  }

  for (let r = 0; r < footers.length; r++) {
    let tr = tfoot.children[r] as HTMLTableRowElement | undefined;
    if (!tr) {
      tr = document.createElement('tr');
      tr.appendChild(document.createElement('td')); // row-number gutter
      tfoot.appendChild(tr);
    } else if (tr.children.length === 0) {
      tr.appendChild(document.createElement('td'));
    }

    const rowVals = footers[r] ?? [];
    const colCount = Math.max(headersLen, rowVals.length);

    while (tr.children.length < colCount + 1) {
      const td = document.createElement('td');
      td.className = 'sg-footer-cell';
      tr.appendChild(td);
    }
    while (tr.children.length > colCount + 1) {
      tr.lastElementChild?.remove();
    }

    for (let c = 0; c < colCount; c++) {
      const td = tr.children[c + 1] as HTMLTableCellElement;
      td.classList.add('sg-footer-cell');
      td.textContent = rowVals[c] ?? '';
    }
  }
}

function resolveToolbar(toolbar: SpreadsheetToolbarConfig | undefined): unknown {
  if (toolbar == null || toolbar === false) return undefined;
  if (toolbar === true) return true;
  if (Array.isArray(toolbar)) return { items: toolbar };
  return toolbar;
}

/**
 * Relative width weight for a column. Prefer content (+ header) over title-only guesses.
 * Explicit `width` is a floor (never clip content below what text needs).
 */
let sgMeasureCtx: CanvasRenderingContext2D | null | undefined;

function getTextMeasureContext(): CanvasRenderingContext2D | null {
  if (sgMeasureCtx !== undefined) return sgMeasureCtx;
  if (typeof document === 'undefined') {
    sgMeasureCtx = null;
    return null;
  }
  const canvas = document.createElement('canvas');
  sgMeasureCtx = canvas.getContext('2d');
  return sgMeasureCtx;
}

function measureTextWidthPx(text: string, font: string): number {
  const t = text.replace(/\s+/g, ' ').trim();
  if (!t) return 0;
  const ctx = getTextMeasureContext();
  if (!ctx) return Math.ceil(Array.from(t).length * 7.5);
  ctx.font = font;
  return ctx.measureText(t.length > 120 ? `${t.slice(0, 120)}…` : t).width;
}

const SG_HEADER_FONT =
  '700 12px ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';
const SG_BODY_FONT =
  '500 13px ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';

function cellValueToMeasureText(value: SpreadsheetCellValue): string {
  if (value == null || value === false) return '';
  if (typeof value === 'boolean') return value ? 'Yes' : '';
  return String(value);
}

function textLooksLikeDisplayedDateTime(text: string): boolean {
  const t = text.replace(/\s+/g, ' ').trim();
  if (!t) return false;
  // Raw ISO/SQL timestamps are storage values — UI often shows date-only via fmtDate.
  // Do not treat them as "time is visible" for column width.
  if (/^\d{4}-\d{2}-\d{2}([T\s]\d{2}:\d{2}(:\d{2})?)?/.test(t) && !/[AaPp][Mm]/.test(t)) {
    // Plain "2026-07-06 14:30" without AM/PM still might be shown — only count if
    // it does not look like a raw ISO (has T or trailing Z / ms).
    if (/^\d{4}-\d{2}-\d{2}T/.test(t) || /Z$/i.test(t) || /\.\d{3}/.test(t)) return false;
    if (/^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}/.test(t)) return true;
    return false;
  }
  // Locale / formatted display that includes a clock ("7/6/2026, 3:45 PM", "06 Jul 2026 03:45")
  if (/\d{1,2}:\d{2}(:\d{2})?(\s*[AaPp][Mm])?/.test(t) && /\d{4}|\d{1,2}[\/.\-]\d{1,2}/.test(t)) {
    return true;
  }
  return false;
}

/**
 * Compact measure text for dates:
 * - Raw ISO/SQL → date only (matches typical fmtDate display)
 * - Already-formatted date+time display strings kept as-is (time must not clip)
 */
function normalizeTextForWidthMeasure(text: string, keepTime = false): string {
  const t = text.replace(/\s+/g, ' ').trim();
  if (!t) return '';
  const iso = t.match(/^(\d{4}-\d{2}-\d{2})[T\s](\d{2}):(\d{2})(?::(\d{2}))?/);
  if (iso) {
    if (keepTime) return `${iso[1]} ${iso[2]}:${iso[3]}`;
    return iso[1];
  }
  if (/^\d{4}-\d{2}-\d{2}$/.test(t)) return t;
  return t;
}

function isDateLikeColumn<T>(
  col: SpreadsheetColumnDef<T>,
  sampleTexts: string[]
): boolean {
  if (col.grow === true) return false;
  const key = String(col.key ?? '').toLowerCase();
  const title = flattenTitle(col.title).toLowerCase();
  if (
    /(^|_)(date|time)(_|$)|(^|_)(created|updated|insert_date|update_date|sale_date|exp_date|expiry|disposal_date|opening_date|order_date|receiving_date)(_|$)/.test(
      key
    )
  ) {
    return true;
  }
  if (/\b(date|created|updated|expiry|datetime)\b/.test(title)) return true;

  let n = 0;
  let dateish = 0;
  for (const raw of sampleTexts) {
    const s = normalizeTextForWidthMeasure(raw);
    if (!s || s === '—' || s === '-') continue;
    n += 1;
    if (
      /^\d{4}-\d{2}-\d{2}(\s+\d{2}:\d{2})?$/.test(s) ||
      /^\d{1,2}[\/.\-]\d{1,2}[\/.\-]\d{2,4}/.test(s) ||
      textLooksLikeDisplayedDateTime(raw)
    ) {
      dateish += 1;
    }
  }
  return n >= 2 && dateish / n >= 0.6;
}

function columnSamplesShowTime(sampleTexts: string[]): boolean {
  return sampleTexts.some((s) => textLooksLikeDisplayedDateTime(s));
}

function preferredColumnWeight(
  title: string | undefined,
  type: SpreadsheetColumnDef<unknown>['type'] | undefined,
  explicit?: number | string
): number {
  if (explicit != null && explicit !== '') {
    const n = typeof explicit === 'number' ? explicit : Number(explicit);
    if (Number.isFinite(n) && n > 0) return n;
  }

  if (type === 'checkbox' || type === 'radio') return 44;
  if (type === 'html') return 72;
  if (type === 'numeric') return 100;

  const label = formatColumnHeaderTitle(title) || flattenTitle(title);
  const fromTitle = Math.ceil(measureTextWidthPx(label, SG_HEADER_FONT) + 24);
  return Math.max(56, Math.min(220, fromTitle));
}

function preferredColumnWeightFromContent<T>(
  col: SpreadsheetColumnDef<T>,
  rows: T[],
  options?: { filters?: boolean }
): number {
  if (col.type === 'checkbox' || col.type === 'radio') return 44;
  if (col.type === 'html') {
    return preferredColumnWeight(col.title, col.type, col.width);
  }

  const filterPad = options?.filters && col.filter !== false ? 16 : 0;
  const pad = 22 + filterPad;
  const minW = 52;
  /** Soft cap — still prefer H-scroll over crushing a single mega-column into oblivion. */
  // Nowrap full-text columns (vendor/name) need a higher ceiling so values aren't ellipsized.
  const maxW = col.wordWrap === false && col.grow === false ? 720 : 480;

  const key = String(col.key ?? '').toLowerCase();
  const title = flattenTitle(col.title).toLowerCase();
  const titleImpliesTime = /\b(time|datetime)\b/.test(title) || /_time$|datetime/.test(key);

  const sampleTexts: string[] = [];
  const rawSamples: string[] = [];
  let maxPx = 0;

  const total = rows.length;
  if (total > 0) {
    const sampleLimit = Math.min(total, 120);
    const step = total <= sampleLimit ? 1 : Math.ceil(total / sampleLimit);
    for (let i = 0; i < total; i += step) {
      const row = rows[i];
      const raw = col.getValue
        ? col.getValue(row, i)
        : (row as Record<string, unknown>)[col.key];
      const rawText = cellValueToMeasureText(raw as SpreadsheetCellValue);
      const text = normalizeTextForWidthMeasure(rawText, titleImpliesTime);
      if (!text) continue;
      rawSamples.push(rawText);
      sampleTexts.push(text);
      maxPx = Math.max(maxPx, measureTextWidthPx(text, SG_BODY_FONT) + pad);
    }
  }

  const dateLike = isDateLikeColumn(col, sampleTexts.length ? sampleTexts : [title]);
  const hasTime = titleImpliesTime || columnSamplesShowTime(rawSamples);

  if (!dateLike) {
    // Non-date: header participates in width.
    maxPx = Math.max(
      maxPx,
      measureTextWidthPx(formatColumnHeaderTitle(col.title) || flattenTitle(col.title), SG_HEADER_FONT) +
        pad
    );
  }

  if (col.type === 'numeric') {
    maxPx = Math.max(maxPx, 64 + pad);
  }

  // Date-only: size to the date cell, not the long header ("Sale Date" / "Created").
  // Header can ellipsis slightly; empty space beside YYYY-MM-DD is worse.
  if (dateLike && !hasTime) {
    const dateBody = measureTextWidthPx('2026-07-17', SG_BODY_FONT) + 18 + filterPad;
    maxPx = Math.max(dateBody, maxPx > 0 ? Math.min(maxPx, dateBody + 4) : dateBody);
  }

  if (dateLike && hasTime) {
    const dateTimePx = measureTextWidthPx('2026-07-17 23:59', SG_BODY_FONT) + pad;
    maxPx = Math.max(maxPx, dateTimePx);
  }

  let w = Math.ceil(maxPx || minW);
  if (dateLike && hasTime) {
    w = Math.max(minW, Math.min(280, w));
  } else if (dateLike) {
    w = Math.max(minW, Math.min(108, w));
  } else {
    w = Math.max(minW, Math.min(maxW, w));
  }

  // Empty sheet: allow author `width` as a seed. Otherwise content wins (no clip / no excess).
  if (total === 0 && col.width != null && col.width !== '') {
    const explicit = typeof col.width === 'number' ? col.width : Number(col.width);
    if (Number.isFinite(explicit) && explicit > 0) {
      return Math.max(minW, Math.floor(explicit));
    }
  }

  return w;
}

type ColumnFitSpec = { weight: number; grow: boolean; lock?: boolean };

function getColumnFitSpecs<T>(
  columns: SpreadsheetColumnDef<T>[],
  actions: SpreadsheetGridProps<T>['actions'] | undefined,
  rows: T[],
  options?: { filters?: boolean }
): ColumnFitSpec[] {
  const specs: ColumnFitSpec[] = columns.map((col) => {
    const weight = preferredColumnWeightFromContent(col, rows, options);
    // grow/lock kept for callers; leftover is shared across all unlocked cols below.
    const grow = col.grow !== false;
    return {
      weight,
      grow,
      // Only force-lock when caller opts out of growth (e.g. fixed icon/date chips).
      lock: col.grow === false,
    };
  });
  if (actions) {
    const fromIcons = preferredColumnWeight(actions.title, 'html', actions.width);
    // Never crush the header label ("Action" / "Actions") when icon count is small.
    const fromHeader = preferredColumnWeight(actions.title, 'text');
    specs.push({
      weight: Math.max(fromIcons, fromHeader),
      // Actions stay content/icon-sized unless explicitly grown.
      grow: actions.grow === true,
      lock: actions.grow !== true,
    });
  }
  return specs;
}

/**
 * Pin the table to the sum of col widths so header/body text cannot force a larger min width.
 * Call after every programmatic or user column resize.
 */
function syncSheetPixelWidth(worksheet: SpreadsheetWorksheetInstance) {
  const table =
    (worksheet.table as HTMLTableElement | undefined) ??
    (worksheet.element as HTMLElement | undefined)?.querySelector?.('table') ??
    null;
  if (!table) return;

  const cols = table.querySelectorAll('colgroup col, col');
  let total = 0;
  if (cols.length > 0) {
    for (let i = 0; i < cols.length; i++) {
      const w = Number.parseInt(cols[i].getAttribute('width') || '0', 10);
      total += Number.isFinite(w) && w > 0 ? w : 0;
    }
  }

  if (total <= 0) {
    // Fallback: headers (includes row-number gutter).
    const headers = table.querySelectorAll('thead tr:first-child td, thead tr:first-child th');
    for (let i = 0; i < headers.length; i++) {
      const cell = headers[i] as HTMLTableCellElement;
      const w =
        Number.parseInt(cell.style.width || cell.getAttribute('width') || '0', 10) ||
        Math.round(cell.getBoundingClientRect().width);
      total += Number.isFinite(w) && w > 0 ? w : 0;
    }
  }

  if (total <= 0) return;

  const px = `${total}px`;
  // Skip no-op writes — they still notify ResizeObserver / MutationObserver and cause blink.
  if (table.style.width !== px) table.style.width = px;
  if (table.style.minWidth !== px) table.style.minWidth = px;
  if (table.style.maxWidth !== px) table.style.maxWidth = px;

  const host = table.closest('.sg-host') as HTMLElement | null;
  if (host && host.style.width !== px) {
    host.style.width = px;
  }
}

/**
 * Fit columns in two steps:
 * 1) Content floor — never narrower than header/cell text (no clipping).
 * 2) If the sheet still has free width, distribute leftover evenly across all
 *    unlocked columns so no single column hogs empty space.
 * If content is wider than the container, keep floors and allow H-scroll.
 */
function fitColumnsToContainer(
  worksheet: SpreadsheetWorksheetInstance,
  container: HTMLElement,
  specs: ColumnFitSpec[],
  programmaticGuard?: { current: boolean }
) {
  const setWidth = worksheet.setWidth as
    | ((column: number, width: number) => void)
    | undefined;
  if (!setWidth || specs.length === 0) return;

  const table =
    (worksheet.table as HTMLTableElement | undefined) ??
    (worksheet.element as HTMLElement | undefined)?.querySelector?.('table') ??
    container.querySelector('table');
  if (!table) return;

  const clientW = container.clientWidth;
  if (clientW <= 0) return;

  const gutterCell = table.querySelector(
    'thead tr:first-child td:first-child, thead tr:first-child th:first-child'
  ) as HTMLTableCellElement | null;
  const gutter = Math.max(gutterCell?.offsetWidth ?? 50, 40);
  const usable = Math.max(specs.length, clientW - gutter - 1);
  const count = specs.length;

  const floors = specs.map((s) => Math.max(1, Math.floor(s.weight)));
  const contentSum = floors.reduce((a, w) => a + w, 0);

  const applyWidths = (widths: number[]) => {
    for (let i = 0; i < widths.length; i++) setWidth(i, widths[i]);
  };

  const prevGuard = programmaticGuard?.current;
  if (programmaticGuard) programmaticGuard.current = true;
  try {
    // Content does not fit — keep floors and allow horizontal scroll (do not crush).
    if (contentSum > usable) {
      applyWidths(floors);
      syncSheetPixelWidth(worksheet);
      return;
    }

    const leftover = usable - contentSum;
    if (leftover <= 0) {
      applyWidths(floors);
      syncSheetPixelWidth(worksheet);
      return;
    }

    // Share leftover evenly across unlocked columns (Actions stay locked by default).
    let expandIdx = specs.map((s, i) => (s.lock ? -1 : i)).filter((i) => i >= 0);
    if (expandIdx.length === 0) {
      expandIdx = specs.map((_, i) => i);
    }

    const widths = floors.slice();
    const baseShare = Math.floor(leftover / expandIdx.length);
    let allocated = 0;
    for (let e = 0; e < expandIdx.length; e++) {
      const i = expandIdx[e];
      if (e === expandIdx.length - 1) {
        widths[i] = floors[i] + Math.max(0, leftover - allocated);
        continue;
      }
      allocated += baseShare;
      widths[i] = floors[i] + baseShare;
    }

    applyWidths(widths);
    syncSheetPixelWidth(worksheet);
  } finally {
    if (programmaticGuard) programmaticGuard.current = prevGuard ?? false;
  }
}

function isStructurallyEditableColumn<T>(
  columns: SpreadsheetColumnDef<T>[],
  colIndex: number
): boolean {
  const col = columns[colIndex];
  if (!col) return false; // actions / out of range → not editable
  if (col.readOnly) return false;
  if (col.customInteract) return true;
  return typeof col.setValue === 'function';
}

function rangeHasOnlyReadonlyColumns<T>(
  columns: SpreadsheetColumnDef<T>[],
  borderLeftIndex: number,
  borderRightIndex: number
): boolean {
  const left = Math.min(Number(borderLeftIndex), Number(borderRightIndex));
  const right = Math.max(Number(borderLeftIndex), Number(borderRightIndex));
  if (!Number.isFinite(left) || !Number.isFinite(right)) return true;
  for (let colIndex = left; colIndex <= right; colIndex++) {
    if (isStructurallyEditableColumn(columns, colIndex)) return false;
  }
  return true;
}

function findNearestEditableColumn<T>(
  columns: SpreadsheetColumnDef<T>[],
  fromIndex: number,
  direction: 1 | -1
): number | null {
  let i = fromIndex + direction;
  while (i >= 0 && i < columns.length) {
    if (isStructurallyEditableColumn(columns, i)) return i;
    i += direction;
  }
  return null;
}

function forceSelectEditableCell(
  instance: SpreadsheetWorksheetInstance,
  colIndex: number,
  rowIndex: number,
  guards: { selection: { current: boolean }; root: HTMLElement | null }
) {
  guards.selection.current = true;
  guards.root?.classList.remove('sg-selection-on-readonly');
  try {
    instance.resetSelection?.();
    instance.updateSelectionFromCoords?.(colIndex, rowIndex, colIndex, rowIndex);
  } finally {
    // Defer clearing so nested onbeforeselection from updateSelection sees the flag.
    queueMicrotask(() => {
      guards.selection.current = false;
      guards.root?.classList.remove('sg-selection-on-readonly');
      scrollWorksheetCellIntoView(guards.root, instance, colIndex, rowIndex);
    });
  }
}

/** Keep keyboard / selection movement inside the visible sheet viewport. */
function resolveSheetScrollContainer(from: HTMLElement): HTMLElement {
  const sheet = from.classList.contains('sg-sheet')
    ? from
    : ((from.querySelector('.sg-sheet') as HTMLElement | null) ??
      (from.closest('.sg-sheet') as HTMLElement | null) ??
      from);

  const canScroll = (el: HTMLElement) => {
    const style = getComputedStyle(el);
    const y =
      (style.overflowY === 'auto' || style.overflowY === 'scroll') &&
      el.scrollHeight > el.clientHeight + 1;
    const x =
      (style.overflowX === 'auto' || style.overflowX === 'scroll') &&
      el.scrollWidth > el.clientWidth + 1;
    return y || x;
  };

  if (canScroll(sheet)) return sheet;

  // fit-content sheets use overflow-y: visible — walk up to the real scroller.
  let node: HTMLElement | null = sheet.parentElement;
  while (node) {
    if (canScroll(node)) return node;
    if (node.classList.contains('sg-root') && node.parentElement) {
      node = node.parentElement;
      continue;
    }
    if (node === document.body) break;
    node = node.parentElement;
  }
  return sheet;
}

/**
 * `.sg-sheet` uses `overflow: auto` (+ overscroll contain). Even when there is
 * nothing to scroll vertically (booking service/deal fit sheets), the browser
 * still treats it as the wheel target and blocks the modal/page scroller.
 * Redirect the delta to the nearest parent scroller when the sheet cannot move.
 */
function attachSheetWheelScrollChain(sheet: HTMLElement): () => void {
  const onWheel = (event: WheelEvent) => {
    if (event.ctrlKey) return; // let browser zoom

    const dy = event.deltaY;
    const dx = event.deltaX;
    if (dy === 0 && dx === 0) return;

    const maxY = Math.max(0, sheet.scrollHeight - sheet.clientHeight);
    const maxX = Math.max(0, sheet.scrollWidth - sheet.clientWidth);
    const atTop = sheet.scrollTop <= 0;
    const atBottom = sheet.scrollTop >= maxY - 1;
    const atLeft = sheet.scrollLeft <= 0;
    const atRight = sheet.scrollLeft >= maxX - 1;

    const dominantY = Math.abs(dy) >= Math.abs(dx);
    const yBlocked =
      dominantY && (maxY <= 0 || (dy < 0 && atTop) || (dy > 0 && atBottom));
    const xBlocked =
      !dominantY && (maxX <= 0 || (dx < 0 && atLeft) || (dx > 0 && atRight));

    if (!yBlocked && !xBlocked) return;

    let node: HTMLElement | null = sheet.parentElement;
    while (node && node !== document.documentElement) {
      const style = getComputedStyle(node);
      const oy = style.overflowY;
      const ox = style.overflowX;
      const canY =
        (oy === 'auto' || oy === 'scroll' || oy === 'overlay') &&
        node.scrollHeight > node.clientHeight + 1;
      const canX =
        (ox === 'auto' || ox === 'scroll' || ox === 'overlay') &&
        node.scrollWidth > node.clientWidth + 1;

      if (yBlocked && canY) {
        node.scrollTop += dy;
        event.preventDefault();
        return;
      }
      if (xBlocked && canX) {
        node.scrollLeft += dx;
        event.preventDefault();
        return;
      }
      node = node.parentElement;
    }
  };

  sheet.addEventListener('wheel', onWheel, { passive: false });
  return () => sheet.removeEventListener('wheel', onWheel);
}

function scrollWorksheetCellIntoView(
  sheetOrRoot: HTMLElement | null,
  instance: SpreadsheetWorksheetInstance,
  colIndex: number,
  rowIndex: number
) {
  if (!sheetOrRoot || !Number.isFinite(colIndex) || !Number.isFinite(rowIndex)) return;

  const cell =
    (instance.getCellFromCoords?.(colIndex, rowIndex) as HTMLElement | null | undefined) ??
    (instance.getCell?.(colIndex, rowIndex) as HTMLElement | null | undefined) ??
    null;
  if (!cell || !cell.isConnected) return;

  const sheet = resolveSheetScrollContainer(sheetOrRoot);
  const scope = sheetOrRoot.closest('.sg-root') ?? sheetOrRoot;
  const sheetRect = sheet.getBoundingClientRect();
  const cellRect = cell.getBoundingClientRect();

  const header = scope.querySelector(
    '.jss_worksheet > thead > tr:not(.jss_filter), .jexcel > thead > tr:not(.jss_filter)'
  ) as HTMLElement | null;
  const footer = scope.querySelector(
    '.jss_worksheet > tfoot, .jexcel > tfoot'
  ) as HTMLElement | null;
  const topInset = header?.getBoundingClientRect().height ?? 0;
  const bottomInset = footer?.getBoundingClientRect().height ?? 0;
  const pad = 6;

  let nextTop = sheet.scrollTop;
  let nextLeft = sheet.scrollLeft;

  const visibleTop = sheetRect.top + topInset + pad;
  const visibleBottom = sheetRect.bottom - bottomInset - pad;
  const visibleLeft = sheetRect.left + pad;
  const visibleRight = sheetRect.right - pad;

  if (cellRect.bottom > visibleBottom) {
    nextTop += cellRect.bottom - visibleBottom;
  } else if (cellRect.top < visibleTop) {
    nextTop -= visibleTop - cellRect.top;
  }

  if (cellRect.right > visibleRight) {
    nextLeft += cellRect.right - visibleRight;
  } else if (cellRect.left < visibleLeft) {
    nextLeft -= visibleLeft - cellRect.left;
  }

  nextTop = Math.max(0, nextTop);
  nextLeft = Math.max(0, nextLeft);
  if (nextTop === sheet.scrollTop && nextLeft === sheet.scrollLeft) return;
  sheet.scrollTo({ top: nextTop, left: nextLeft });
}

function resolveSelectionFocusCell(
  left: number,
  top: number,
  right: number,
  bottom: number,
  prev: { left: number; right: number; top: number; bottom: number } | null
): { x: number; y: number } {
  if (!prev) return { x: left, y: top };
  if (bottom > prev.bottom) return { x: right, y: bottom };
  if (top < prev.top) return { x: left, y: top };
  if (right > prev.right) return { x: right, y: bottom };
  if (left < prev.left) return { x: left, y: top };
  if (top !== prev.top || left !== prev.left) return { x: left, y: top };
  return { x: right, y: bottom };
}

function rowsToSheetData<T>(
  rows: T[],
  columns: SpreadsheetColumnDef<T>[],
  actions?: SpreadsheetGridProps<T>['actions']
): Record<string, string | number | boolean>[] {
  return rows.map((row, rowIndex) => {
    const obj: Record<string, string | number | boolean> = {};
    for (const col of columns) {
      const raw = col.getValue
        ? col.getValue(row, rowIndex)
        : (row as Record<string, unknown>)[col.key];
      obj[col.key] = toCellValue(raw as SpreadsheetCellValue);
    }
    if (actions) {
      obj[ACTIONS_KEY] = actions.renderHtml(row, rowIndex);
    }
    return obj;
  });
}

function buildJssColumns<T>(
  columns: SpreadsheetColumnDef<T>[],
  actions: SpreadsheetGridProps<T>['actions'] | undefined,
  dataRef: MutableRefObject<T[]>,
  columnsRef: MutableRefObject<SpreadsheetColumnDef<T>[]>
): Record<string, unknown>[] {
  const cols: Record<string, unknown>[] = columns.map((col, colIndex) => {
    const title = formatColumnHeaderTitle(col.title) || col.title;
    return {
      type: col.type ?? 'text',
      title,
      name: col.key,
      width: preferredColumnWeightFromContent(col, dataRef.current),
      align: col.align ?? 'left',
      readOnly: col.readOnly ?? false,
      wordWrap: col.wordWrap,
      mask: col.mask,
      decimal: col.decimal,
      source: col.source,
      autocomplete: col.autocomplete,
      multiple: col.multiple,
      ...(col.filter != null ? { filter: col.filter } : {}),
      ...col.jss,
      render: (
        cell: HTMLTableCellElement,
        value: SpreadsheetCellValue,
        x: number,
        y: number,
        instance: SpreadsheetWorksheetInstance
      ) => {
        const rowIndex = Number(y);
        const index = Number.isFinite(Number(x)) ? Number(x) : colIndex;
        const row = dataRef.current[rowIndex];
        const currentCol = columnsRef.current[index] ?? col;
        if (row && currentCol?.render) {
          currentCol.render(cell, value, row, rowIndex, instance);
        }
      },
    };
  });

  if (actions) {
    const actionTitle = formatColumnHeaderTitle(actions.title) || actions.title || '';
    cols.push({
      type: 'html',
      title: actionTitle,
      name: ACTIONS_KEY,
      width: preferredColumnWeight(actionTitle, 'html', actions.width),
      align: actions.align ?? 'center',
      readOnly: true,
      // Icon-only Actions column — no Excel value filter.
      filter: false,
    });
  }

  return cols;
}

function serializeDataSignature<T>(
  rows: T[],
  columns: SpreadsheetColumnDef<T>[],
  actions?: SpreadsheetGridProps<T>['actions']
): string {
  return JSON.stringify(rowsToSheetData(rows, columns, actions));
}

export default function SpreadsheetGrid<T>({
  columns,
  data,
  className = '',
  disabled = false,
  emptyMessage = 'No rows.',
  showWhenEmpty = false,
  minDimensions,
  /**
   * Prefer false when the grid sits in a flex layout — parent `.sg-root` scrolls instead.
   * Use true only with a pixel `tableHeight` (e.g. 400), not `%`.
   */
  tableOverflow = false,
  /**
   * Optional jss viewport width. Leave unset — we size columns in JS so the sheet
   * fills on load, and users can still expand/collapse past those defaults.
   */
  tableWidth,
  tableHeight,
  allowInsertRow = false,
  allowDeleteRow = false,
  allowInsertColumn = false,
  allowDeleteColumn = false,
  allowManualInsertRow = false,
  allowManualInsertColumn = false,
  columnSorting = false,
  columnDrag = false,
  rowDrag = false,
  columnResize = true,
  rowResize = false,
  search = false,
  pagination,
  selectionCopy = true,
  defaultColAlign = 'left',
  wordWrap = true,
  filters = false,
  serverFilters = false,
  columnFilters,
  onColumnFilterChange,
  toolbar,
  footers,
  renderToolbar,
  renderFooter,
  downloadFileName,
  lockReadonlyInteraction = true,
  getRowClassName,
  getCellError,
  onDataChange,
  onCellChange,
  onDeleteRows,
  actions,
  onActionClick,
  gridRef,
  worksheetOptions,
  spreadsheetOptions,
}: SpreadsheetGridProps<T>) {
  const reactId = useId();
  const rootRef = useRef<HTMLDivElement | null>(null);
  const hostRef = useRef<HTMLDivElement | null>(null);
  const worksheetRef = useRef<SpreadsheetWorksheetInstance | null>(null);
  const rootElRef = useRef<HTMLElement | null>(null);
  const isProgrammaticSet = useRef(false);
  /** True while a cell editor is open — parent-driven setData must not wipe in-progress typing. */
  const isEditingRef = useRef(false);
  const pendingPushWhileEditingRef = useRef(false);
  const programmaticSetClearTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isProgrammaticResize = useRef(false);
  /** After the user manually resizes a column, stop auto-fitting so expand/collapse sticks. */
  const userResizedColumnsRef = useRef(false);
  /** Last container width we fit for — ignore tiny ResizeObserver noise / scrollbar jitter. */
  const lastFitContainerWidthRef = useRef(0);
  const colWidthObserverRef = useRef<MutationObserver | null>(null);
  const isProgrammaticSelection = useRef(false);
  const lastSelectionRef = useRef<{
    left: number;
    right: number;
    top: number;
    bottom: number;
  } | null>(null);
  const searchQueryRef = useRef('');
  /** Session-only cell styles (toolbar format) — keyed as "x,y". */
  const sessionCellStylesRef = useRef(new Map<string, Record<string, string>>());
  const mountedConfigRef = useRef('');
  const lastPushedSignatureRef = useRef('');

  const dataRef = useRef(data);
  const columnsRef = useRef(columns);
  const actionsRef = useRef(actions);
  const onDataChangeRef = useRef(onDataChange);
  const onCellChangeRef = useRef(onCellChange);
  const onDeleteRowsRef = useRef(onDeleteRows);
  const onActionClickRef = useRef(onActionClick);
  const getRowClassNameRef = useRef(getRowClassName);
  const getCellErrorRef = useRef(getCellError);
  const disabledRef = useRef(disabled);
  const columnFiltersRef = useRef(columnFilters ?? {});
  const onColumnFilterChangeRef = useRef(onColumnFilterChange);
  const serverFiltersRef = useRef(serverFilters);
  const optionsRef = useRef({
    minDimensions,
    tableOverflow,
    tableWidth,
    tableHeight,
    allowInsertRow,
    allowDeleteRow,
    allowInsertColumn,
    allowDeleteColumn,
    allowManualInsertRow,
    allowManualInsertColumn,
    columnSorting,
    columnDrag,
    rowDrag,
    columnResize,
    rowResize,
    search,
    suppressNativeSearch: typeof renderToolbar === 'function',
    pagination,
    selectionCopy,
    defaultColAlign,
    wordWrap,
    filters,
    toolbar,
    lockReadonlyInteraction,
    worksheetOptions,
    spreadsheetOptions,
  });

  dataRef.current = data;
  columnsRef.current = columns;
  actionsRef.current = actions;
  onDataChangeRef.current = onDataChange;
  onCellChangeRef.current = onCellChange;
  onDeleteRowsRef.current = onDeleteRows;
  onActionClickRef.current = onActionClick;
  getRowClassNameRef.current = getRowClassName;
  getCellErrorRef.current = getCellError;
  disabledRef.current = disabled;
  columnFiltersRef.current = columnFilters ?? {};
  onColumnFilterChangeRef.current = onColumnFilterChange;
  serverFiltersRef.current = serverFilters;
  optionsRef.current = {
    minDimensions,
    tableOverflow,
    tableWidth,
    tableHeight,
    allowInsertRow,
    allowDeleteRow,
    allowInsertColumn,
    allowDeleteColumn,
    allowManualInsertRow,
    allowManualInsertColumn,
    columnSorting,
    columnDrag,
    rowDrag,
    columnResize,
    rowResize,
    search,
    suppressNativeSearch: typeof renderToolbar === 'function',
    pagination,
    selectionCopy,
    defaultColAlign,
    wordWrap,
    filters,
    toolbar,
    lockReadonlyInteraction,
    worksheetOptions,
    spreadsheetOptions,
  };

  const dataSignature = useMemo(
    () => serializeDataSignature(data, columns, actions),
    [data, columns, actions]
  );

  const columnsStructureKey = useMemo(
    () =>
      JSON.stringify(
        columns.map((c) => ({
          key: c.key,
          title: c.title,
          type: c.type,
          width: c.width,
          readOnly: c.readOnly,
          customInteract: c.customInteract,
          align: c.align,
          filter: c.filter,
          grow: c.grow,
        }))
      ) +
      (actions ? '|actions' : '') +
      `|filters:${filters ? 1 : 0}` +
      `|toolbar:${JSON.stringify(resolveToolbar(toolbar) ?? null)}` +
      // Function footers stay mounted; array footers remount when the template changes.
      `|footers:${
        typeof footers === 'function' ? 'fn' : JSON.stringify(footers ?? null)
      }` +
      `|search:${search ? 1 : 0}` +
      `|suppressNativeSearch:${typeof renderToolbar === 'function' ? 1 : 0}` +
      `|pagination:${pagination ?? 0}`,
    [actions, columns, filters, footers, pagination, renderToolbar, search, toolbar]
  );

  const applyRowClasses = useCallback(() => {
    const worksheet = worksheetRef.current;
    if (!worksheet || !getRowClassNameRef.current) return;
    const rows = dataRef.current;
    for (let i = 0; i < rows.length; i++) {
      const className = getRowClassNameRef.current(rows[i], i);
      const cell =
        worksheet.getCellFromCoords?.(0, i) ?? worksheet.getCell?.(0, i);
      const tr = cell?.parentElement;
      if (!tr) continue;
      tr.classList.remove('sg-row-dimmed');
      if (className) {
        for (const token of className.split(/\s+/).filter(Boolean)) {
          tr.classList.add(token);
        }
      }
    }
  }, []);

  const applyCellErrors = useCallback(() => {
    const worksheet = worksheetRef.current;
    if (!worksheet) return;
    const rows = dataRef.current;
    const cols = columnsRef.current;
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex++) {
      const row = rows[rowIndex];
      for (let colIndex = 0; colIndex < cols.length; colIndex++) {
        const col = cols[colIndex];
        const cell =
          worksheet.getCellFromCoords?.(colIndex, rowIndex) ??
          worksheet.getCell?.(colIndex, rowIndex);
        if (!cell) continue;
        const message = getCellErrorRef.current?.(row, rowIndex, col.key);
        if (message) {
          cell.classList.add('sg-cell-error');
          cell.title = message;
          cell.setAttribute('aria-invalid', 'true');
          cell.setAttribute('data-sg-error', message);
        } else {
          cell.classList.remove('sg-cell-error');
          // Preserve intentional titles from column render (e.g. est. price source).
          if (cell.getAttribute('data-sg-error')) {
            cell.removeAttribute('title');
            cell.removeAttribute('data-sg-error');
          }
          cell.removeAttribute('aria-invalid');
        }
      }
    }
  }, []);

  const BODY_ROW_HEIGHT_PX = 26;

  const applyRowHeights = useCallback(() => {
    const worksheet = worksheetRef.current;
    if (!worksheet) return;
    const setHeight = (worksheet as { setHeight?: (row: number, height: number) => void }).setHeight;
    if (!setHeight) return;

    const prev = isProgrammaticResize.current;
    isProgrammaticResize.current = true;
    try {
      for (let rowIndex = 0; rowIndex < dataRef.current.length; rowIndex++) {
        setHeight(rowIndex, BODY_ROW_HEIGHT_PX);
      }
    } finally {
      isProgrammaticResize.current = prev;
    }
  }, []);

  const applyEditableChrome = useCallback(() => {
    const worksheet = worksheetRef.current;
    if (!worksheet) return;
    const cols = columnsRef.current;
    const rows = dataRef.current;
    const hasActions = !!actionsRef.current;
    const totalCols = cols.length + (hasActions ? 1 : 0);

    const table =
      worksheet.table ??
      (worksheet.element as HTMLElement | undefined)?.querySelector?.('table') ??
      null;
    if (!table) return;

    const headers = table.querySelectorAll('thead tr:first-child td');
    for (let i = 1; i < headers.length; i++) {
      const colIndex = i - 1;
      const editable = isStructurallyEditableColumn(cols, colIndex);
      const header = headers[i] as HTMLTableCellElement;
      header.classList.toggle('sg-col-editable', editable);
      header.classList.toggle('sg-col-readonly', !editable);
      const col = cols[colIndex];
      const fullTitle = col
        ? formatColumnHeaderTitle(col.title) || col.title
        : formatColumnHeaderTitle(actionsRef.current?.title) ||
          actionsRef.current?.title ||
          '';
      if (fullTitle && fullTitle !== '\u00A0') {
        header.title = fullTitle;
        // Keep visible header text in Title Case without wiping filter/funnel chrome.
        const textNode = Array.from(header.childNodes).find(
          (n) => n.nodeType === Node.TEXT_NODE && (n.textContent ?? '').trim()
        );
        if (textNode) {
          textNode.textContent = fullTitle;
        } else if (
          !header.querySelector('.sg-excel-filter') &&
          header.childElementCount === 0
        ) {
          header.textContent = fullTitle;
        }
      }
    }

    for (let rowIndex = 0; rowIndex < rows.length; rowIndex++) {
      const row = rows[rowIndex];
      for (let colIndex = 0; colIndex < totalCols; colIndex++) {
        const cell =
          worksheet.getCellFromCoords?.(colIndex, rowIndex) ??
          worksheet.getCell?.(colIndex, rowIndex);
        if (!cell) continue;
        const col = cols[colIndex];
        if (col?.key === '_selected') {
          cell.classList.add('sg-cell-select');
          cell.classList.toggle('sg-cell-editable', true);
          cell.classList.toggle('sg-cell-readonly', false);
          continue;
        }
        const structurallyEditable = isStructurallyEditableColumn(cols, colIndex);
        const rowLocked = !!(col && row && col.isReadOnly?.(row, rowIndex));
        const editable = structurallyEditable && !rowLocked;
        cell.classList.toggle('sg-cell-editable', editable);
        cell.classList.toggle('sg-cell-readonly', !editable);

        // Hover full value when ellipsis clips body text (skip action/html cells).
        if (col && col.type !== 'checkbox' && col.type !== 'html' && !cell.querySelector('button, input, svg')) {
          const tip = (cell.textContent ?? '').replace(/\s+/g, ' ').trim();
          if (tip && !cell.title) cell.title = tip;
        }
      }
    }
  }, []);

  const layoutColumns = useCallback((forceFit = false) => {
    const ws = worksheetRef.current;
    const root = rootRef.current;
    if (!ws || !root) return;
    // Don't wipe the user's column resize (that would hide columns again / kill H-scroll).
    if (userResizedColumnsRef.current && !forceFit) return;
    // Avoid re-entry while setWidth / sync is already running.
    if (isProgrammaticResize.current && !forceFit) return;

    const clientW = root.clientWidth;
    if (
      !forceFit &&
      lastFitContainerWidthRef.current > 0 &&
      Math.abs(clientW - lastFitContainerWidthRef.current) < 4
    ) {
      return;
    }

    userResizedColumnsRef.current = false;
    lastFitContainerWidthRef.current = clientW;
    fitColumnsToContainer(
      ws,
      root,
      getColumnFitSpecs(columnsRef.current, actionsRef.current, dataRef.current, {
        filters: !!optionsRef.current.filters,
      }),
      isProgrammaticResize
    );
  }, []);

  const reapplySessionCellStyles = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws || sessionCellStylesRef.current.size === 0) return;
    for (const [key, styles] of sessionCellStylesRef.current) {
      const [xs, ys] = key.split(',');
      const x = Number(xs);
      const y = Number(ys);
      if (!Number.isFinite(x) || !Number.isFinite(y)) continue;
      const cell =
        ws.getCellFromCoords?.(x, y) ?? ws.getCell?.(x, y) ?? null;
      if (!cell) continue;
      for (const [prop, val] of Object.entries(styles)) {
        cell.style.setProperty(prop, val, 'important');
      }
    }
  }, []);

  /** Re-run custom column.render after CE recreates/mutates cells (DataTable React mounts). */
  const reapplyCustomRenders = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return;
    const cols = columnsRef.current;
    const rows = dataRef.current;
    for (let y = 0; y < rows.length; y++) {
      const row = rows[y];
      for (let x = 0; x < cols.length; x++) {
        const col = cols[x];
        if (!col?.render) continue;
        const cell =
          ws.getCellFromCoords?.(x, y) ?? ws.getCell?.(x, y) ?? null;
        if (!cell) continue;
        const raw = col.getValue
          ? col.getValue(row, y)
          : (row as Record<string, unknown>)[col.key];
        col.render(cell, toCellValue(raw as SpreadsheetCellValue), row, y, ws);
      }
    }
  }, []);

  const decorateSheet = useCallback(() => {
    const ws = worksheetRef.current;
    if (ws) {
      sgSheetRowCount.set(ws, dataRef.current.length);
    }
    if (ws?.options?.filters) {
      coerceWorksheetDataMatrixForFilters(ws);
      if (serverFiltersRef.current) {
        sgServerFilterBridge.set(ws, {
          values: { ...columnFiltersRef.current },
          onChange: (key, value) => {
            onColumnFilterChangeRef.current?.(key, value);
          },
          keyByCol: (colIndex) => {
            const col = columnsRef.current[colIndex];
            if (!col || col.filter === false) return null;
            return col.filterKey ?? col.key;
          },
        });
      } else {
        sgServerFilterBridge.delete(ws);
      }
      wireExcelStyleHeaderFilters(ws);
    }
    reapplyCustomRenders();
    applyRowHeights();
    applyEditableChrome();
    applyRowClasses();
    applyCellErrors();
    reapplySessionCellStyles();
    if (ws) applySgSheetSearch(ws, searchQueryRef.current);
  }, [
    applyCellErrors,
    applyEditableChrome,
    applyRowClasses,
    applyRowHeights,
    reapplyCustomRenders,
    reapplySessionCellStyles,
  ]);

  const pushSheetData = useCallback(
    (rows: T[], force = false) => {
      const ws = worksheetRef.current;
      if (!ws) return;
      const signature = serializeDataSignature(rows, columnsRef.current, actionsRef.current);
      if (!force && signature === lastPushedSignatureRef.current) {
        decorateSheet();
        return;
      }
      // Never clobber an open editor (batch-loading / status / action HTML sync).
      if (!force && isEditingRef.current) {
        pendingPushWhileEditingRef.current = true;
        return;
      }
      lastPushedSignatureRef.current = signature;
      isProgrammaticSet.current = true;
      if (programmaticSetClearTimerRef.current != null) {
        clearTimeout(programmaticSetClearTimerRef.current);
        programmaticSetClearTimerRef.current = null;
      }
      sgSheetRowCount.set(ws, rows.length);
      ws.setData(rowsToSheetData(rows, columnsRef.current, actionsRef.current));
      // CE may emit onafterchanges asynchronously after setData — keep the guard longer
      // than a single microtask so empty/spurious commits cannot wipe typed values.
      programmaticSetClearTimerRef.current = setTimeout(() => {
        programmaticSetClearTimerRef.current = null;
        isProgrammaticSet.current = false;
        decorateSheet();
      }, 50);
      decorateSheet();
    },
    [decorateSheet]
  );

  const pushSheetDataRef = useRef(pushSheetData);
  pushSheetDataRef.current = pushSheetData;

  /** Apply only the reported cell changes — do not re-read getData() (CE often returns arrays). */
  const applyCellChanges = useCallback((changes: SheetCellChange[]) => {
    const cols = columnsRef.current;
    const nextData = [...dataRef.current];
    const cellChanges: SpreadsheetCellChange<T>[] = [];

    for (const ch of changes) {
      const colIndex = Number(ch.x);
      const rowIndex = Number(ch.y);
      if (!Number.isFinite(colIndex) || !Number.isFinite(rowIndex)) continue;
      const col = cols[colIndex];
      if (!col || col.key === ACTIONS_KEY || col.readOnly || !col.setValue) continue;
      const row = nextData[rowIndex];
      if (!row || col.isReadOnly?.(row, rowIndex)) continue;

      const updated = col.setValue(row, ch.value, rowIndex);
      nextData[rowIndex] = updated;
      cellChanges.push({
        rowIndex,
        columnKey: col.key,
        columnIndex: colIndex,
        value: ch.value,
        oldValue: ch.oldValue,
        row: updated,
      });
    }

    return { nextData, cellChanges };
  }, []);

  const destroyGrid = useCallback(() => {
    closeSgColumnMenus();
    colWidthObserverRef.current?.disconnect();
    colWidthObserverRef.current = null;
    if (programmaticSetClearTimerRef.current != null) {
      clearTimeout(programmaticSetClearTimerRef.current);
      programmaticSetClearTimerRef.current = null;
    }
    isProgrammaticSet.current = false;
    isEditingRef.current = false;
    pendingPushWhileEditingRef.current = false;
    if (rootElRef.current) {
      try {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        jspreadsheet.destroy(rootElRef.current as any, true);
      } catch {
        /* already destroyed */
      }
      rootElRef.current = null;
    }
    worksheetRef.current = null;
    mountedConfigRef.current = '';
    if (hostRef.current) {
      hostRef.current.innerHTML = '';
      hostRef.current.style.width = '';
    }
  }, []);

  const initGrid = useCallback(() => {
    const host = hostRef.current;
    if (!host || typeof window === 'undefined') return;

    destroyGrid();

    const opts = optionsRef.current;
    const sheetData = rowsToSheetData(dataRef.current, columnsRef.current, actionsRef.current);
    const jssColumns = buildJssColumns(
      columnsRef.current,
      actionsRef.current,
      dataRef,
      columnsRef
    );
    const minCols = Math.max(jssColumns.length, 1);
    const minRows = Math.max(sheetData.length, 1);

    const resolvedToolbar = resolveToolbar(opts.toolbar);
    const footersForInit = resolveFooters(
      footers,
      dataRef.current,
      columnsRef.current
    );

    const worksheets = jspreadsheet(
      host,
      {
        worksheets: [
          {
            data: sheetData,
            columns: jssColumns,
            minDimensions: opts.minDimensions ?? [minCols, minRows],
            tableOverflow: opts.tableOverflow,
            // Only pin size when caller asks. Avoid `%` heights — they break vertical scroll in flex parents.
            ...(opts.tableWidth != null ? { tableWidth: opts.tableWidth } : {}),
            ...(opts.tableHeight != null ? { tableHeight: opts.tableHeight } : {}),
            allowInsertRow: opts.allowInsertRow,
            allowDeleteRow: opts.allowDeleteRow || !!onDeleteRowsRef.current,
            allowInsertColumn: opts.allowInsertColumn,
            allowDeleteColumn: opts.allowDeleteColumn,
            allowManualInsertRow: opts.allowManualInsertRow,
            allowManualInsertColumn: opts.allowManualInsertColumn,
            columnSorting: opts.columnSorting,
            columnDrag: opts.columnDrag,
            rowDrag: opts.rowDrag,
            columnResize: opts.columnResize,
            rowResize: opts.rowResize,
            search: Boolean(opts.search) && !opts.suppressNativeSearch,
            pagination: opts.pagination,
            selectionCopy: opts.selectionCopy,
            defaultColAlign: opts.defaultColAlign,
            wordWrap: opts.wordWrap,
            filters: opts.filters,
            ...(footersForInit ? { footers: footersForInit } : {}),
            editable: !disabledRef.current,
            ...opts.worksheetOptions,
          },
        ],
        // Totals footers are plain text from the app — don't turn on the formula parser.
        parseFormulas: false,
        ...(resolvedToolbar != null ? { toolbar: resolvedToolbar } : {}),
        ...opts.spreadsheetOptions,
        // Hide CE "Save as..." — exports go through our toolbar download when needed.
        allowExport: false,
        // CE native copy puts cell innerHTML on the clipboard; force plain text.
        oncopy: (
          instance: SpreadsheetWorksheetInstance,
          coords: [number, number, number, number],
          data: string,
          cut?: boolean
        ) => {
          const userOnCopy = opts.spreadsheetOptions?.oncopy as
            | ((
                i: SpreadsheetWorksheetInstance,
                c: [number, number, number, number],
                d: string,
                cutFlag?: boolean
              ) => string | false | void)
            | undefined;
          const plain =
            coords?.length === 4
              ? buildSelectionPlainTsv(
                  instance,
                  Number(coords[0]),
                  Number(coords[1]),
                  Number(coords[2]),
                  Number(coords[3])
                )
              : stripHtmlToPlainText(String(data ?? ''));
          if (typeof userOnCopy === 'function') {
            const overridden = userOnCopy(instance, coords, plain, cut);
            if (overridden === false) return false;
            if (typeof overridden === 'string') return overridden;
          }
          return plain;
        },
        // Drop "Save as..." from the right-click menu if CE still injects it.
        contextMenu: (
          instance: SpreadsheetWorksheetInstance,
          x: number | null,
          y: number | null,
          e: MouseEvent,
          items: Array<{ title?: string; type?: string; [key: string]: unknown }>,
          section: string,
          ...rest: unknown[]
        ) => {
          const userMenu = opts.spreadsheetOptions?.contextMenu as
            | ((
                i: SpreadsheetWorksheetInstance,
                col: number | null,
                row: number | null,
                ev: MouseEvent,
                menuItems: Array<{ title?: string; type?: string; [key: string]: unknown }>,
                role: string,
                ...extra: unknown[]
              ) => typeof items | false | void)
            | undefined;

          const cleaned = items.filter((item) => {
            const title = String(item.title ?? '')
              .replace(/\u2026|\.\.\./g, '')
              .trim()
              .toLowerCase();
            if (!title) return true;
            // Remove CE "Save as..." export entry from the right-click menu.
            return !title.startsWith('save as');
          });

          if (typeof userMenu === 'function') {
            const result = userMenu(instance, x, y, e, cleaned, section, ...rest);
            if (result === false) return false;
            if (Array.isArray(result)) return result;
          }
          return cleaned;
        },
        onbeforechange: (
          instance: SpreadsheetWorksheetInstance,
          cell: HTMLTableCellElement,
          colIndex: string | number,
          rowIndex: string | number,
          newValue: SpreadsheetCellValue
        ) => {
          if (disabledRef.current) {
            return instance.getValue?.(colIndex, rowIndex) ?? newValue;
          }
          const col = columnsRef.current[Number(colIndex)];
          if (!col) return newValue;
          if (col.readOnly) {
            return instance.getValue?.(colIndex, rowIndex) ?? newValue;
          }
          const row = dataRef.current[Number(rowIndex)];
          if (row && col.isReadOnly?.(row, Number(rowIndex))) {
            return instance.getValue?.(colIndex, rowIndex) ?? newValue;
          }

          let value = newValue;
          // Numeric columns: only allow numeric commits (letters never stick).
          // Skip when a jSuites mask is configured — mask owns formatting.
          if (col.type === 'numeric' && !col.mask) {
            const sep = getNumericDecimalSep(col.decimal);
            const raw = newValue == null ? '' : String(newValue);
            if (raw.trim() === '') {
              value = '';
            } else {
              const cleaned = coerceNumericCommitValue(newValue, sep);
              // Pure garbage (e.g. "abc") → keep previous cell value.
              if (cleaned === '' && sanitizeNumericDraft(raw, sep) === '') {
                return instance.getValue?.(colIndex, rowIndex) ?? '';
              }
              value = cleaned;
            }
          }

          const customBefore = opts.spreadsheetOptions?.onbeforechange as
            | ((
                i: SpreadsheetWorksheetInstance,
                c: HTMLTableCellElement,
                x: string | number,
                y: string | number,
                v: SpreadsheetCellValue
              ) => SpreadsheetCellValue | undefined)
            | undefined;
          const custom = customBefore?.(instance, cell, colIndex, rowIndex, value);
          return custom !== undefined ? custom : value;
        },
        oncreateeditor: (
          instance: SpreadsheetWorksheetInstance,
          cell: HTMLTableCellElement,
          colIndex: number,
          rowIndex: number,
          editor: unknown,
          columnOptions: unknown
        ) => {
          const customCreate = opts.spreadsheetOptions?.oncreateeditor as
            | ((
                i: SpreadsheetWorksheetInstance,
                c: HTMLTableCellElement,
                x: number,
                y: number,
                ed: unknown,
                colOpts: unknown
              ) => void)
            | undefined;
          customCreate?.(instance, cell, colIndex, rowIndex, editor, columnOptions);

          const col = columnsRef.current[Number(colIndex)];
          if (!col || col.type !== 'numeric' || col.mask) return;

          const input = findEditorTextControl(cell, editor);
          if (!input) return;
          bindNumericEditorInput(input, getNumericDecimalSep(col.decimal));
        },
        onafterchanges: (instance: SpreadsheetWorksheetInstance, changes: SheetCellChange[]) => {
          if (isProgrammaticSet.current || disabledRef.current) return;
          const { nextData, cellChanges } = applyCellChanges(changes);
          if (cellChanges.length === 0) return;

          dataRef.current = nextData;
          // Mark signature so the React→sheet sync effect does not clobber mid-edit.
          lastPushedSignatureRef.current = serializeDataSignature(
            nextData,
            columnsRef.current,
            actionsRef.current
          );
          onCellChangeRef.current?.(cellChanges, nextData);
          onDataChangeRef.current?.(nextData);

          const customAfter = opts.spreadsheetOptions?.onafterchanges as
            | ((i: SpreadsheetWorksheetInstance, c: SheetCellChange[]) => void)
            | undefined;
          customAfter?.(instance, changes);
        },
        onbeforedeleterow: (instance: SpreadsheetWorksheetInstance, removedRows: number[]) => {
          if (onDeleteRowsRef.current) {
            onDeleteRowsRef.current(removedRows.map(Number));
            return false;
          }
          const customDelete = opts.spreadsheetOptions?.onbeforedeleterow as
            | ((i: SpreadsheetWorksheetInstance, rows: number[]) => boolean | undefined)
            | undefined;
          return customDelete?.(instance, removedRows);
        },
        onbeforeselection: (
          instance: SpreadsheetWorksheetInstance,
          borderLeftIndex: number,
          borderTopIndex: number,
          borderRightIndex: number,
          borderBottomIndex: number,
          origin: Event | undefined
        ) => {
          if (isProgrammaticSelection.current) return undefined;

          const customSel = opts.spreadsheetOptions?.onbeforeselection as
            | ((
                i: SpreadsheetWorksheetInstance,
                l: number,
                t: number,
                r: number,
                b: number,
                o: Event | undefined
              ) => false | undefined)
            | undefined;
          const customResult = customSel?.(
            instance,
            borderLeftIndex,
            borderTopIndex,
            borderRightIndex,
            borderBottomIndex,
            origin
          );
          if (customResult === false) return false;

          if (opts.lockReadonlyInteraction === false) return undefined;

          const cols = columnsRef.current;
          const left = Math.min(Number(borderLeftIndex), Number(borderRightIndex));
          const right = Math.max(Number(borderLeftIndex), Number(borderRightIndex));
          const top = Math.min(Number(borderTopIndex), Number(borderBottomIndex));
          const bottom = Math.max(Number(borderTopIndex), Number(borderBottomIndex));

          // Editable target (or mixed range that includes editable) — allow as-is.
          if (!rangeHasOnlyReadonlyColumns(cols, left, right)) return undefined;

          // Readonly landing: cancel and jump directly to nearest editable column.
          const prev = lastSelectionRef.current;
          let direction: 1 | -1 = 1;
          if (prev) {
            const prevCenter = (prev.left + prev.right) / 2;
            const nextCenter = (left + right) / 2;
            direction = nextCenter >= prevCenter ? 1 : -1;
          }
          const searchFrom = prev
            ? direction === 1
              ? Math.max(prev.left, prev.right)
              : Math.min(prev.left, prev.right)
            : left;

          const jumpTo =
            findNearestEditableColumn(cols, searchFrom, direction) ??
            findNearestEditableColumn(cols, searchFrom, direction === 1 ? -1 : 1);
          if (jumpTo == null) return false;

          // Cancel readonly landing, then force a visible selection on the editable target.
          queueMicrotask(() => {
            forceSelectEditableCell(instance, jumpTo, top, {
              selection: isProgrammaticSelection,
              root: rootRef.current,
            });
            // Keep vertical span when user had multi-row selection intent.
            if (bottom !== top) {
              isProgrammaticSelection.current = true;
              try {
                instance.updateSelectionFromCoords?.(jumpTo, top, jumpTo, bottom);
              } finally {
                queueMicrotask(() => {
                  isProgrammaticSelection.current = false;
                });
              }
            }
          });
          return false;
        },
        onselection: (
          instance: SpreadsheetWorksheetInstance,
          borderLeftIndex: number,
          borderTopIndex: number,
          borderRightIndex: number,
          borderBottomIndex: number,
          origin?: Event
        ) => {
          const left = Math.min(Number(borderLeftIndex), Number(borderRightIndex));
          const right = Math.max(Number(borderLeftIndex), Number(borderRightIndex));
          const top = Math.min(Number(borderTopIndex), Number(borderBottomIndex));
          const bottom = Math.max(Number(borderTopIndex), Number(borderBottomIndex));
          const prev = lastSelectionRef.current;
          const focus = resolveSelectionFocusCell(left, top, right, bottom, prev);

          lastSelectionRef.current = { left, right, top, bottom };
          rootRef.current?.classList.remove('sg-selection-on-readonly');
          // Keep keyboard focus on the sheet so Enter can open customInteract pickers.
          try {
            rootRef.current?.focus({ preventScroll: true });
          } catch {
            /* ignore */
          }

          // Keyboard (and any selection that leaves the viewport) must bring the cell into view.
          // rootRef is the `.sg-sheet` scroller itself — wait a frame so CE finishes moving selection chrome.
          queueMicrotask(() => {
            requestAnimationFrame(() => {
              scrollWorksheetCellIntoView(rootRef.current, instance, focus.x, focus.y);
            });
          });

          const customOnSel = opts.spreadsheetOptions?.onselection as
            | ((
                i: SpreadsheetWorksheetInstance,
                l: number,
                t: number,
                r: number,
                b: number,
                o?: Event
              ) => void)
            | undefined;
          customOnSel?.(
            instance,
            borderLeftIndex,
            borderTopIndex,
            borderRightIndex,
            borderBottomIndex,
            origin
          );
        },
        oneditionstart: (
          instance: SpreadsheetWorksheetInstance,
          td: HTMLTableCellElement,
          colIndex: number,
          rowIndex: number
        ) => {
          isEditingRef.current = true;

          const customEdit = opts.spreadsheetOptions?.oneditionstart as
            | ((
                i: SpreadsheetWorksheetInstance,
                cell: HTMLTableCellElement,
                x: number,
                y: number
              ) => void)
            | undefined;

          const col = columnsRef.current[Number(colIndex)];
          const row = dataRef.current[Number(rowIndex)];

          // Custom cell UI (e.g. date picker): never open native text editor.
          const rowLocked =
            !!col?.readOnly || !!(row && col?.isReadOnly?.(row, Number(rowIndex)));

          if (col?.customInteract) {
            isEditingRef.current = false;
            instance.closeEditor?.(td, false);
            if (!rowLocked && row) {
              queueMicrotask(() => {
                if (col.onActivate) {
                  col.onActivate(row, Number(rowIndex), td);
                  return;
                }
                if (!activateSpreadsheetCustomInteract(td)) {
                  const trigger = td.querySelector(
                    '[data-sg-custom-interact], .sg-sheet-datetime-trigger'
                  ) as HTMLElement | null;
                  trigger?.click();
                }
              });
            }
            customEdit?.(instance, td, colIndex, rowIndex);
            return;
          }

          // Row-level isReadOnly must block the editor even when the column is
          // structurally editable (has setValue). Otherwise locked cells feel editable.
          if (rowLocked && opts.lockReadonlyInteraction !== false) {
            isEditingRef.current = false;
            instance.closeEditor?.(td, false);
            const next =
              findNearestEditableColumn(columnsRef.current, Number(colIndex), 1) ??
              findNearestEditableColumn(columnsRef.current, Number(colIndex), -1);
            if (next != null) {
              queueMicrotask(() => {
                forceSelectEditableCell(instance, next, Number(rowIndex), {
                  selection: isProgrammaticSelection,
                  root: rootRef.current,
                });
              });
            }
            customEdit?.(instance, td, colIndex, rowIndex);
            return;
          }

          if (
            opts.lockReadonlyInteraction === false ||
            isStructurallyEditableColumn(columnsRef.current, Number(colIndex))
          ) {
            customEdit?.(instance, td, colIndex, rowIndex);
            return;
          }

          // Safety: never edit readonly — jump to nearest editable.
          isEditingRef.current = false;
          instance.closeEditor?.(td, false);
          const next =
            findNearestEditableColumn(columnsRef.current, Number(colIndex), 1) ??
            findNearestEditableColumn(columnsRef.current, Number(colIndex), -1);
          if (next == null) return;

          queueMicrotask(() => {
            forceSelectEditableCell(instance, next, Number(rowIndex), {
              selection: isProgrammaticSelection,
              root: rootRef.current,
            });
          });
        },
        oneditionend: (
          instance: SpreadsheetWorksheetInstance,
          td: HTMLTableCellElement,
          colIndex: number,
          rowIndex: number,
          editorValue: SpreadsheetCellValue,
          wasSaved: boolean
        ) => {
          isEditingRef.current = false;
          if (pendingPushWhileEditingRef.current) {
            pendingPushWhileEditingRef.current = false;
            // Flush parent-driven sheet sync that was deferred while typing.
            queueMicrotask(() => {
              if (!isEditingRef.current) {
                pushSheetDataRef.current(dataRef.current);
              }
            });
          }

          const customEnd = opts.spreadsheetOptions?.oneditionend as
            | ((
                i: SpreadsheetWorksheetInstance,
                cell: HTMLTableCellElement,
                x: number,
                y: number,
                v: SpreadsheetCellValue,
                saved: boolean
              ) => void)
            | undefined;
          customEnd?.(instance, td, colIndex, rowIndex, editorValue, wasSaved);

          if (opts.lockReadonlyInteraction === false) return;

          // Wait until CE + readonly-skip redirects settle, then paint a visible selection.
          window.setTimeout(() => {
            const cols = columnsRef.current;
            const sel = instance.getSelection?.();
            let x = sel ? Number(sel[0]) : Number(colIndex);
            let y = sel ? Number(sel[1]) : Number(rowIndex);

            if (!isStructurallyEditableColumn(cols, x)) {
              const jumpTo =
                findNearestEditableColumn(cols, Number(colIndex), 1) ??
                findNearestEditableColumn(cols, Number(colIndex), -1);
              if (jumpTo == null) return;
              x = jumpTo;
            }

            forceSelectEditableCell(instance, x, y, {
              selection: isProgrammaticSelection,
              root: rootRef.current,
            });
          }, 0);
        },
        onresizecolumn: (
          instance: SpreadsheetWorksheetInstance,
          colIndex: number | number[],
          newWidth: number | number[],
          oldWidth: number | number[]
        ) => {
          // setWidth() re-fires this event — skip while we adjust widths ourselves.
          if (isProgrammaticResize.current) return;

          // Column expand/collapse (resize) is allowed on every column, including read-only.
          const customResize = opts.spreadsheetOptions?.onresizecolumn as
            | ((
                i: SpreadsheetWorksheetInstance,
                c: number | number[],
                n: number | number[],
                o: number | number[]
              ) => void)
            | undefined;
          customResize?.(instance, colIndex, newWidth, oldWidth);

          // Keep the user's expand/collapse; don't re-fit (that would snap widths back).
          userResizedColumnsRef.current = true;
          // Shrink table with the column so header/body text cannot prop the column open.
          syncSheetPixelWidth(instance);
        },
        onresizerow: (
          instance: SpreadsheetWorksheetInstance,
          rowIndex: number,
          _newHeight: number,
          oldHeight: number
        ) => {
          if (isProgrammaticResize.current) return;

          // Row height changes are disabled for this grid chrome.
          if (opts.rowResize === false && Number.isFinite(oldHeight)) {
            const setHeight = (instance as { setHeight?: (r: number, h: number) => void }).setHeight;
            isProgrammaticResize.current = true;
            try {
              setHeight?.(rowIndex, oldHeight);
            } finally {
              isProgrammaticResize.current = false;
            }
          }
          const customRowResize = opts.spreadsheetOptions?.onresizerow as
            | ((i: SpreadsheetWorksheetInstance, r: number, n: number, o: number) => void)
            | undefined;
          customRowResize?.(instance, rowIndex, _newHeight, oldHeight);
        },
      } as unknown as Parameters<typeof jspreadsheet>[1]
    );

    const first = Array.isArray(worksheets) ? worksheets[0] : worksheets;
    worksheetRef.current = first as unknown as SpreadsheetWorksheetInstance;
    rootElRef.current = host;
    mountedConfigRef.current = columnsStructureKey;
    lastPushedSignatureRef.current = serializeDataSignature(
      dataRef.current,
      columnsRef.current,
      actionsRef.current
    );
    decorateSheet();
    if (footersForInit) {
      applyWorksheetFooters(
        first as unknown as SpreadsheetWorksheetInstance,
        footersForInit
      );
    }

    // Live column drag updates col width attrs without firing onresizecolumn until mouseup.
    colWidthObserverRef.current?.disconnect();
    const tableEl =
      (first as unknown as SpreadsheetWorksheetInstance).table ??
      host.querySelector('table');
    const colgroup = tableEl?.querySelector('colgroup');
    if (colgroup && typeof MutationObserver !== 'undefined') {
      const observer = new MutationObserver(() => {
        // Programmatic setWidth during fit must not mark user-resize or thrash sync.
        if (isProgrammaticResize.current) return;
        // Mark immediately (before mouseup) so ResizeObserver cannot re-fit mid-drag.
        userResizedColumnsRef.current = true;
        const ws = worksheetRef.current;
        if (ws) syncSheetPixelWidth(ws);
      });
      observer.observe(colgroup, {
        attributes: true,
        subtree: true,
        attributeFilter: ['width'],
      });
      colWidthObserverRef.current = observer;
    }

    requestAnimationFrame(() => layoutColumns(true));
    // Side panels animate open (~300ms) — re-fit once the final width is settled.
    window.setTimeout(() => {
      if (worksheetRef.current) layoutColumns(true);
    }, 340);
  }, [
    applyCellChanges,
    columnsStructureKey,
    decorateSheet,
    destroyGrid,
    footers,
    layoutColumns,
  ]);

  const resolveColumnIndex = useCallback((columnKeyOrIndex: string | number) => {
    if (typeof columnKeyOrIndex === 'number') return columnKeyOrIndex;
    const idx = columnsRef.current.findIndex((c) => c.key === columnKeyOrIndex);
    return idx >= 0 ? idx : -1;
  }, []);

  const openFilter = useCallback(
    (columnKeyOrIndex: string | number) => {
      const ws = worksheetRef.current;
      if (!ws) return;
      const idx = resolveColumnIndex(columnKeyOrIndex);
      if (idx < 0) return;
      coerceWorksheetDataMatrixForFilters(ws);
      const table = ws.table;
      const titleTd = table?.querySelector(
        `thead tr:not(.jss_filter) > td:nth-child(${idx + 2})`
      ) as HTMLElement | null;
      if (titleTd) {
        openSgColumnFilterMenu(ws, idx, titleTd);
        return;
      }
      // Fallback: still open menu anchored to the funnel if present.
      const funnel = table?.querySelector(
        `.sg-excel-filter[data-col="${idx}"]`
      ) as HTMLElement | null;
      if (funnel) openSgColumnFilterMenu(ws, idx, funnel);
    },
    [resolveColumnIndex]
  );

  const resetFilters = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return;
    getSgColumnFilterState(ws).clear();
    reapplySgColumnFilters(ws);
    ws.resetFilters?.();
  }, []);

  const getActiveColumnIndex = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return 0;
    const sel = ws.getSelection?.();
    if (sel && Number.isFinite(sel[0])) return Math.max(0, Number(sel[0]));
    const cached = lastSelectionRef.current;
    if (cached) return Math.max(0, cached.left);
    return 0;
  }, []);

  const undo = useCallback(() => {
    worksheetRef.current?.undo?.();
  }, []);

  const redo = useCallback(() => {
    worksheetRef.current?.redo?.();
  }, []);

  const cut = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return;
    if (typeof ws.cut === 'function') ws.cut();
    else ws.copy?.(true);
  }, []);

  const copy = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return;

    // Prefer live selection; fall back to last onselection (toolbar click can blur CE).
    const live = ws.getSelection?.();
    const cached = lastSelectionRef.current;
    const x1 = live ? Number(live[0]) : cached?.left;
    const y1 = live ? Number(live[1]) : cached?.top;
    const x2 = live ? Number(live[2]) : cached?.right;
    const y2 = live ? Number(live[3]) : cached?.bottom;
    if (
      x1 == null ||
      y1 == null ||
      x2 == null ||
      y2 == null ||
      !Number.isFinite(x1) ||
      !Number.isFinite(y1) ||
      !Number.isFinite(x2) ||
      !Number.isFinite(y2)
    ) {
      return;
    }

    const xa = Math.min(x1, x2);
    const xb = Math.max(x1, x2);
    const ya = Math.min(y1, y2);
    const yb = Math.max(y1, y2);

    // Re-apply highlight so CE native copy / visual cue still work after blur.
    try {
      ws.updateSelectionFromCoords?.(xa, ya, xb, yb);
    } catch {
      /* ignore */
    }

    const tsv = buildSelectionPlainTsv(ws, xa, ya, xb, yb);
    if (!tsv) return;

    const writeViaTextarea = () => {
      const ta =
        (ws as SpreadsheetWorksheetInstance & { textarea?: HTMLTextAreaElement })
          .textarea ?? document.createElement('textarea');
      const owned = !(ws as { textarea?: HTMLTextAreaElement }).textarea;
      if (owned) {
        ta.setAttribute('readonly', '');
        ta.style.position = 'fixed';
        ta.style.left = '-9999px';
        document.body.appendChild(ta);
      }
      ta.value = tsv;
      ta.focus();
      ta.select();
      try {
        document.execCommand('copy');
      } catch {
        /* ignore */
      } finally {
        if (owned) document.body.removeChild(ta);
      }
    };

    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      void navigator.clipboard.writeText(tsv).catch(() => {
        writeViaTextarea();
      });
      return;
    }

    writeViaTextarea();
  }, []);

  const paste = useCallback(() => {
    const ws = worksheetRef.current;
    if (!ws) return;
    if (typeof ws.paste === 'function') ws.paste();
    else {
      try {
        document.execCommand('paste');
      } catch {
        /* ignore — browsers often block programmatic paste */
      }
    }
  }, []);

  const searchSheet = useCallback((query: string) => {
    const ws = worksheetRef.current;
    if (!ws) return;
    searchQueryRef.current = query;
    applySgSheetSearch(ws, query);
  }, []);

  const clearSearch = useCallback(() => {
    const ws = worksheetRef.current;
    searchQueryRef.current = '';
    if (!ws) return;
    applySgSheetSearch(ws, '');
  }, []);

  const sortActiveColumn = useCallback(
    (direction: 0 | 1) => {
      const ws = worksheetRef.current;
      if (!ws) return;
      sortWorksheetColumn(ws, getActiveColumnIndex(), direction);
    },
    [getActiveColumnIndex]
  );

  const applyStyle = useCallback((property: string, value: string) => {
    const ws = worksheetRef.current;
    if (!ws) return;
    const live = ws.getSelection?.();
    const cached = lastSelectionRef.current;
    const x1 = live ? Number(live[0]) : cached?.left;
    const y1 = live ? Number(live[1]) : cached?.top;
    const x2 = live ? Number(live[2]) : cached?.right;
    const y2 = live ? Number(live[3]) : cached?.bottom;
    if (
      x1 == null ||
      y1 == null ||
      x2 == null ||
      y2 == null ||
      !Number.isFinite(x1) ||
      !Number.isFinite(y1) ||
      !Number.isFinite(x2) ||
      !Number.isFinite(y2)
    ) {
      return;
    }
    const xa = Math.min(x1, x2);
    const xb = Math.max(x1, x2);
    const ya = Math.min(y1, y2);
    const yb = Math.max(y1, y2);

    // Keep highlight in sync with lastSelection when toolbar stole focus earlier.
    try {
      ws.updateSelectionFromCoords?.(xa, ya, xb, yb);
    } catch {
      /* ignore */
    }

    for (let y = ya; y <= yb; y++) {
      for (let x = xa; x <= xb; x++) {
        const cell =
          ws.getCellFromCoords?.(x, y) ?? ws.getCell?.(x, y) ?? null;
        if (!cell) continue;

        try {
          ws.setStyle?.(cell, property, value);
        } catch {
          try {
            ws.setStyle?.(`${x},${y}`, property, value);
          } catch {
            /* ignore */
          }
        }

        // Theme CSS uses !important on td color/background — beat it with important inline.
        cell.style.setProperty(property, value, 'important');

        const key = `${x},${y}`;
        const prev = sessionCellStylesRef.current.get(key) ?? {};
        sessionCellStylesRef.current.set(key, { ...prev, [property]: value });
      }
    }
  }, []);

  const downloadCsv = useCallback(() => {
    const ws = worksheetRef.current;
    const table = ws?.table;
    if (!table) return;

    const headerCells = table.querySelectorAll(
      'thead tr:not(.jss_filter) > td, thead tr:not(.jss_filter) > th'
    );
    const headers: string[] = [];
    headerCells.forEach((cell, i) => {
      if (i === 0) return; // row index gutter
      const clone = cell.cloneNode(true) as HTMLElement;
      clone.querySelectorAll('.sg-excel-filter, .sg-excel-sort').forEach((el) => el.remove());
      headers.push((clone.textContent ?? '').replace(/\s+/g, ' ').trim());
    });
    if (headers.length === 0) return;

    const headerStyle = {
      font: { bold: true, color: { rgb: '374151' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F3F4F6' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        top: { style: 'thin', color: { rgb: 'D1D5DB' } },
        bottom: { style: 'thin', color: { rgb: 'D1D5DB' } },
        left: { style: 'thin', color: { rgb: 'D1D5DB' } },
        right: { style: 'thin', color: { rgb: 'D1D5DB' } },
      },
    };

    const aoa: (string | number)[][] = [headers];
    const cellStyles = new Map<string, Record<string, unknown>>();

    headers.forEach((_, c) => {
      cellStyles.set(`0,${c}`, headerStyle);
    });

    table.querySelectorAll('tbody > tr').forEach((tr) => {
      if (
        tr.classList.contains('sg-row-filtered-out') ||
        tr.classList.contains('sg-row-search-hidden')
      ) {
        return;
      }
      const y = Number(tr.getAttribute('data-y'));
      const cells = tr.querySelectorAll(':scope > td');
      const vals: string[] = [];
      const excelRow = aoa.length;

      cells.forEach((cell, i) => {
        if (i === 0) return;
        const el = cell as HTMLElement;
        const text = (el.textContent ?? '').replace(/\s+/g, ' ').trim();
        vals.push(text);

        const colIndex = i - 1;
        const session =
          Number.isFinite(y)
            ? sessionCellStylesRef.current.get(`${colIndex},${y}`)
            : undefined;
        const merged: Record<string, string> = {
          ...readDomCellCssProps(el),
          ...(session ?? {}),
        };
        const xStyle = cssPropsToExcelStyle(merged);
        if (xStyle) {
          cellStyles.set(`${excelRow},${colIndex}`, {
            ...xStyle,
            border: {
              top: { style: 'thin', color: { rgb: 'E5E7EB' } },
              bottom: { style: 'thin', color: { rgb: 'E5E7EB' } },
              left: { style: 'thin', color: { rgb: 'E5E7EB' } },
              right: { style: 'thin', color: { rgb: 'E5E7EB' } },
            },
          });
        }
      });

      if (vals.length) aoa.push(vals);
    });

    const sheet = XLSX.utils.aoa_to_sheet(aoa);
    for (const [key, style] of cellStyles) {
      const [r, c] = key.split(',').map(Number);
      const addr = XLSX.utils.encode_cell({ r, c });
      const existing = sheet[addr] as XLSX.CellObject | undefined;
      if (!existing) continue;
      sheet[addr] = { ...existing, s: style as XLSX.CellObject['s'] };
    }

    sheet['!cols'] = headers.map((h, i) => {
      let max = h.length;
      for (let r = 1; r < aoa.length; r++) {
        const v = aoa[r]?.[i];
        max = Math.max(max, String(v ?? '').length);
      }
      return { wch: Math.min(48, Math.max(10, max + 2)) };
    });

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, sheet, 'Sheet1');
    const array = XLSX.write(wb, {
      bookType: 'xlsx',
      type: 'array',
      cellStyles: true,
    }) as ArrayBuffer;

    const blob = new Blob([array], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    const stem = (downloadFileName || 'sheet').replace(/[^\w\-]+/g, '_');
    a.href = url;
    a.download = `${stem}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }, [downloadFileName]);

  const chromeApi = useMemo<SpreadsheetChromeApi<T>>(
    () => ({
      getWorksheet: () => worksheetRef.current,
      data,
      disabled,
      downloadFileName,
      openFilter,
      resetFilters,
      refresh: () => pushSheetData(dataRef.current, true),
      undo,
      redo,
      cut,
      copy,
      paste,
      search: searchSheet,
      clearSearch,
      sortActiveColumn,
      applyStyle,
      downloadCsv,
    }),
    [
      applyStyle,
      clearSearch,
      copy,
      cut,
      data,
      disabled,
      downloadCsv,
      downloadFileName,
      openFilter,
      paste,
      pushSheetData,
      redo,
      resetFilters,
      searchSheet,
      sortActiveColumn,
      undo,
    ]
  );

  const validationSignature = useMemo(() => {
    if (!getCellError) return '';
    return data
      .map((row, rowIndex) =>
        columns.map((col) => getCellError(row, rowIndex, col.key) ?? '').join('\u001f')
      )
      .join('\u001e');
  }, [columns, data, getCellError]);

  useEffect(() => {
    if (!showWhenEmpty && data.length === 0) {
      destroyGrid();
      return;
    }
    if (!hostRef.current) return;
    if (!worksheetRef.current || mountedConfigRef.current !== columnsStructureKey) {
      initGrid();
    }
  }, [columnsStructureKey, data.length, destroyGrid, initGrid, showWhenEmpty]);

  useEffect(() => {
    if (!worksheetRef.current) return;
    // Parent-driven updates (and recomputed read-only columns) flow back through setData.
    pushSheetData(data);
  }, [data, dataSignature, pushSheetData]);

  useEffect(() => {
    const ws = worksheetRef.current;
    if (!ws || !serverFilters) return;
    const bridge = sgServerFilterBridge.get(ws);
    if (!bridge) return;
    bridge.values = { ...(columnFilters ?? {}) };
    paintSgServerFilterIndicators(ws);
  }, [columnFilters, serverFilters]);

  // Debounced content-width refit — avoid setWidth on every stock/label tick (wipes action HTML).
  useEffect(() => {
    if (!worksheetRef.current || userResizedColumnsRef.current) return;
    const t = window.setTimeout(() => {
      if (!worksheetRef.current || userResizedColumnsRef.current) return;
      layoutColumns(true);
    }, 120);
    return () => window.clearTimeout(t);
  }, [dataSignature, layoutColumns]);

  // render / getValue closures update without a structure remount — remount React cell UIs.
  useEffect(() => {
    if (!worksheetRef.current) return;
    reapplyCustomRenders();
  }, [columns, reapplyCustomRenders]);

  // Refresh CE footers whenever data / footer template changes (no remount).
  useEffect(() => {
    const ws = worksheetRef.current;
    if (!ws) return;
    const next = resolveFooters(footers, data, columns);
    applyWorksheetFooters(ws, next);
  }, [columns, data, dataSignature, footers]);

  // Don't trap modal/page wheel when the sheet itself has no vertical scroll.
  useEffect(() => {
    const sheet = rootRef.current;
    if (!sheet) return;
    return attachSheetWheelScrollChain(sheet);
  }, [dataSignature]);

  // Enter / F2 on a focused customInteract cell (date / datetime) opens the picker.
  useEffect(() => {
    const sheet = rootRef.current;
    if (!sheet) return;
    const root = sheet.closest('.sg-root') as HTMLElement | null;
    if (!root) return;

    const onPointerDown = () => {
      root.dataset.sgKeyboardActive = '1';
    };
    const onDocPointerDown = (event: PointerEvent) => {
      const t = event.target as Node | null;
      if (t && root.contains(t)) return;
      delete root.dataset.sgKeyboardActive;
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Enter' && event.key !== 'F2') return;
      if (event.ctrlKey || event.metaKey || event.altKey) return;
      if (disabledRef.current) return;
      if (isSheetDatePickerOpen()) return;
      if (document.querySelector('[data-booking-datetime-picker-panel]')) return;
      if (isEditingRef.current) return;
      if (root.dataset.sgKeyboardActive !== '1') return;

      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.tagName === 'SELECT' ||
          target.isContentEditable)
      ) {
        // Allow native CE editor / inputs; skip only when typing outside our sheet.
        if (!root.contains(target)) return;
        // Inside sheet but in a real input (filter, etc.) — don't hijack.
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') return;
      }

      const ws = worksheetRef.current;
      if (!ws) return;
      const sel = ws.getSelection?.();
      if (!sel || sel.length < 4) return;
      const colIndex = Number(sel[0]);
      const rowIndex = Number(sel[1]);
      if (!Number.isFinite(colIndex) || !Number.isFinite(rowIndex)) return;

      const col = columnsRef.current[colIndex];
      if (!col?.customInteract) return;
      const row = dataRef.current[rowIndex];
      if (!row) return;
      if (col.readOnly || col.isReadOnly?.(row, rowIndex)) return;

      const cell =
        ws.getCellFromCoords?.(colIndex, rowIndex) ??
        ws.getCell?.(colIndex, rowIndex) ??
        null;
      if (!cell) return;

      event.preventDefault();
      event.stopPropagation();
      if (col.onActivate) {
        col.onActivate(row, rowIndex, cell);
        return;
      }
      if (!activateSpreadsheetCustomInteract(cell)) {
        const trigger = cell.querySelector(
          '[data-sg-custom-interact], .sg-sheet-datetime-trigger'
        ) as HTMLElement | null;
        trigger?.click();
      }
    };

    root.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('pointerdown', onDocPointerDown, true);
    document.addEventListener('keydown', onKeyDown, true);
    return () => {
      root.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('pointerdown', onDocPointerDown, true);
      document.removeEventListener('keydown', onKeyDown, true);
    };
  }, [dataSignature]);

  // Errors/touched can change without cell values changing — re-paint validation state.
  useEffect(() => {
    if (!worksheetRef.current) return;
    applyCellErrors();
  }, [applyCellErrors, validationSignature]);

  useEffect(() => {
    const root = rootRef.current;
    if (!root || typeof ResizeObserver === 'undefined') return;

    let frame = 0;
    const observer = new ResizeObserver(() => {
      // Mid-drag / after user resize: never auto-fit (that was the blink).
      if (userResizedColumnsRef.current || isProgrammaticResize.current) return;
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        if (userResizedColumnsRef.current || isProgrammaticResize.current) return;
        layoutColumns();
      });
    });
    observer.observe(root);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, [layoutColumns]);

  useEffect(() => {
    const ws = worksheetRef.current;
    if (!ws?.options) return;
    ws.options.editable = !disabled;
  }, [disabled]);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) return;

    const onClick = (event: MouseEvent) => {
      const target = event.target as HTMLElement | null;
      const btn = target?.closest?.('[data-sg-action]') as HTMLElement | null;
      if (!btn || !host.contains(btn)) return;
      event.preventDefault();
      event.stopPropagation();
      const action = btn.getAttribute('data-sg-action');
      // Allow selection / view-only links even when the sheet is not editable.
      const allowWhenDisabled =
        action === 'toggle-select' || action === 'view-services';
      if (disabledRef.current && !allowWhenDisabled) return;
      const rowAttr = btn.getAttribute('data-sg-row');
      if (!action || rowAttr == null) return;
      const rowIndex = Number(rowAttr);
      const row = dataRef.current[rowIndex];
      if (!row) return;
      onActionClickRef.current?.(action, rowIndex, row);
    };

    host.addEventListener('click', onClick);
    return () => host.removeEventListener('click', onClick);
  }, []);

  useEffect(() => () => destroyGrid(), [destroyGrid]);

  useImperativeHandle(
    gridRef,
    (): SpreadsheetGridHandle => ({
      getWorksheet: () => worksheetRef.current,
      getData: () =>
        (worksheetRef.current?.getData(false) as Record<string, SpreadsheetCellValue>[]) ?? [],
      refresh: () => pushSheetData(dataRef.current, true),
      openFilter,
      resetFilters,
    })
  );

  const showEmpty = !showWhenEmpty && data.length === 0;
  const toolbarNode = renderToolbar?.(chromeApi);
  const footerNode = renderFooter?.(chromeApi);

  return (
    <div
      className={`sg-root flex min-h-0 flex-1 flex-col ${className}`.trim()}
      data-spreadsheet-grid={reactId}
    >
      {toolbarNode ? <div className="sg-chrome-toolbar shrink-0">{toolbarNode}</div> : null}
      <div
        ref={rootRef}
        tabIndex={-1}
        className={`sg-sheet min-h-0 flex-1 outline-none ${showEmpty ? 'sg-sheet-empty' : ''}`.trim()}
      >
        {showEmpty ? <div className="sg-empty">{emptyMessage}</div> : null}
        <div
          ref={hostRef}
          className={showEmpty ? 'hidden' : 'sg-host min-h-0 w-full'}
          aria-hidden={showEmpty}
        />
      </div>
      {footerNode ? <div className="sg-chrome-footer shrink-0">{footerNode}</div> : null}
    </div>
  );
}
