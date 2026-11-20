"use client";

/**
 * ==========================================================================================
 * WHAT: Dashboard — SA multi-company rollup; Tenant Admin company ops + stock DataTable.
 * WHY: TA does not need company-list UX; they need own KPIs + actionable low-stock listing.
 * EXAMPLE: TA Amna @ TGL → KPIs + Stock DataTable (filters/pagination) + Open Stock.
 * ==========================================================================================
 */

import Link from "next/link";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { getDashboard, listStock } from "@/lib/api/endpoints";
import { Alert } from "@/components/ui/alert";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { sessionStore } from "@/lib/auth/session";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";
import {
  btnPrimaryClass,
  btnSecondaryClass,
  pageCanvasFillClass,
} from "@/lib/forms/field-styles";
import type {
  DashboardDto,
  DashboardInventoryRowDto,
  DashboardLowStockItemDto,
  StockItemDto,
} from "@/types/api";

const ALL_COMPANIES = 0;

export function DashboardPage() {
  const role = (sessionStore.getRole() ?? "").toUpperCase();
  const isSuperAdmin = role === "SUPER_ADMIN";
  return isSuperAdmin ? <SuperAdminDashboard /> : <TenantDashboard />;
}

/* ─── Super Admin ─────────────────────────────────────────────────────────── */

function SuperAdminDashboard() {
  const displayName = sessionStore.getFormattedDisplayName();

  const [data, setData] = useState<DashboardDto | null>(null);
  const [rows, setRows] = useState<DashboardInventoryRowDto[]>([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState<number>(ALL_COMPANIES);
  const [includeEmpty, setIncludeEmpty] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [pageSize, setPageSize] = useState(10);
  const [pageIndex, setPageIndex] = useState(0);
  const [cursorStack, setCursorStack] = useState<Array<number | null>>([null]);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);

  const loadPage = useCallback(
    async (afterId: number | null) => {
      setLoading(true);
      setLoadError(null);
      try {
        const dto = await getDashboard({
          companyId: selectedCompanyId > 0 ? selectedCompanyId : undefined,
          includeEmpty,
          afterId: afterId ?? undefined,
          limit: pageSize,
        });
        setData(dto);
        setRows(dto.inventory ?? []);
        setTotalCount(dto.inventoryTotalCount ?? 0);
        setNextCursor(dto.inventoryNextCursor ?? null);
        setHasMore(Boolean(dto.inventoryHasMore));
      } catch {
        setLoadError(
          "We could not load the dashboard. Please refresh, or sign in again if this continues.",
        );
        setData(null);
        setRows([]);
      } finally {
        setLoading(false);
      }
    },
    [selectedCompanyId, includeEmpty, pageSize],
  );

  useEffect(() => {
    setCursorStack([null]);
    setPageIndex(0);
    void loadPage(null);
  }, [loadPage]);

  async function goNext() {
    if (!hasMore || nextCursor == null) return;
    const after = nextCursor;
    setCursorStack((s) => [...s, after]);
    setPageIndex((i) => i + 1);
    try {
      await loadPage(after);
    } catch {
      setLoadError("Unable to load next page.");
    }
  }

  async function goPrev() {
    if (pageIndex <= 0) return;
    const nextStack = cursorStack.slice(0, -1);
    const after = nextStack[nextStack.length - 1] ?? null;
    setCursorStack(nextStack);
    setPageIndex((i) => i - 1);
    try {
      await loadPage(after);
    } catch {
      setLoadError("Unable to load previous page.");
    }
  }

  const companyOptions = useMemo(() => {
    const opts = (data?.companyOptions ?? []).map((c) => ({
      value: c.companyId,
      label: c.companyName,
    }));
    return [{ value: ALL_COMPANIES, label: "All companies" }, ...opts];
  }, [data?.companyOptions]);

  const totals = data?.totals;
  const lowStock = data?.lowStockItems ?? [];
  const lowCount = totals?.lowStockProductCount ?? 0;
  const isFiltered = selectedCompanyId > 0;
  const selectedCompanyLabel =
    data?.scopedCompanyName ??
    companyOptions.find((o) => o.value === selectedCompanyId)?.label ??
    null;
  const greeting = isFiltered
    ? `Showing ${selectedCompanyLabel}`
    : "Overview of active companies";
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  const columns = useMemo<DataTableColumn<DashboardInventoryRowDto>[]>(
    () => [
      {
        key: "companyName",
        label: "Company",
        width: 200,
        render: (r) => (
          <button
            type="button"
            className="truncate text-left font-semibold text-[#2563EB] hover:underline"
            title="Show only this company"
            onClick={() => setSelectedCompanyId(r.companyId)}
          >
            {r.companyName}
          </button>
        ),
      },
      {
        key: "tenantCode",
        label: "Code",
        width: 100,
        render: (r) => (
          <span className="font-mono text-xs text-[#1A2238]">{r.tenantCode}</span>
        ),
      },
      {
        key: "activeProductCount",
        label: "Products",
        width: 100,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {r.activeProductCount}
          </span>
        ),
      },
      {
        key: "totalQuantityOnHand",
        label: "Stock qty",
        width: 110,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {Number(r.totalQuantityOnHand).toLocaleString()}
          </span>
        ),
      },
      {
        key: "lowStockProductCount",
        label: "Low stock",
        width: 100,
        filter: false,
        render: (r) =>
          r.lowStockProductCount > 0 ? (
            <span className="font-semibold tabular-nums text-[#DC2626]">
              {r.lowStockProductCount}
            </span>
          ) : (
            <span className="font-semibold text-[#15803D]">OK</span>
          ),
      },
      {
        key: "activeVendorCount",
        label: "Vendors",
        width: 90,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {r.activeVendorCount}
          </span>
        ),
      },
    ],
    [],
  );

  return (
    <section className={pageCanvasFillClass("gap-3")} aria-label="Dashboard">
      <DashboardHeader
        displayName={displayName}
        greeting={greeting}
        lowCount={lowCount}
        loading={loading}
        onRefresh={() => {
          setCursorStack([null]);
          setPageIndex(0);
          void loadPage(null);
        }}
      >
        <div className="flex min-w-0 flex-1 items-center gap-1.5 rounded-lg border border-white/15 bg-white/95 p-1 shadow-sm backdrop-blur-sm sm:min-w-55">
          <span className="hidden shrink-0 pl-1.5 text-[10px] font-bold uppercase tracking-wider text-[#64748B] sm:inline">
            Find
          </span>
          <div className="min-w-0 flex-1 [&_button]:h-8 [&_button]:px-2 [&_button]:text-xs [&_.rounded-lg]:rounded-md">
            <SearchableSelect
              aria-label="Find a company by name"
              options={companyOptions}
              value={selectedCompanyId}
              emptyValues={[]}
              placeholder="Company name…"
              searchPlaceholder="Search…"
              onChange={(v) =>
                setSelectedCompanyId(Number(v) || ALL_COMPANIES)
              }
            />
          </div>
          {isFiltered ? (
            <button
              type="button"
              title="Clear company filter"
              className="mr-0.5 shrink-0 rounded-md px-1.5 py-1 text-[11px] font-semibold text-[#2563EB] hover:bg-[#EFF6FF]"
              onClick={() => setSelectedCompanyId(ALL_COMPANIES)}
            >
              Clear
            </button>
          ) : null}
        </div>

        <button
          type="button"
          role="switch"
          aria-checked={includeEmpty}
          title="Also list companies with no products yet"
          className={[
            "inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border px-2.5 text-[11px] font-semibold transition",
            includeEmpty
              ? "border-[#93C5FD] bg-[#2563EB] text-white shadow-sm"
              : "border-white/20 bg-white/10 text-[#E2E8F0] hover:bg-white/15",
          ].join(" ")}
          onClick={() => setIncludeEmpty((v) => !v)}
        >
          <span
            aria-hidden
            className={[
              "h-1.5 w-1.5 rounded-full",
              includeEmpty ? "bg-white" : "bg-[#94A3B8]",
            ].join(" ")}
          />
          Empty
        </button>
      </DashboardHeader>

      {loadError ? (
        <LoadError message={loadError} onRetry={() => void loadPage(null)} />
      ) : null}

      <KpiStrip
        items={[
          { label: "Companies", value: totals?.companyCount ?? 0 },
          { label: "Products", value: totals?.activeProductCount ?? 0 },
          { label: "Stock qty", value: totals?.totalQuantityOnHand ?? 0 },
          { label: "Low stock", value: lowCount, danger: lowCount > 0 },
          { label: "Vendors", value: totals?.activeVendorCount ?? 0 },
          {
            label: "Alerts",
            value: totals?.pendingLowStockAlerts ?? 0,
            danger: (totals?.pendingLowStockAlerts ?? 0) > 0,
          },
        ]}
      />

      <QuickNav
        links={[
          { href: "/stock", label: "Stock", highlight: lowCount > 0 },
          { href: "/sales", label: "Sales" },
          { href: "/products", label: "Products" },
          { href: "/vendors", label: "Vendors" },
          { href: "/settings/company", label: "Companies" },
        ]}
      />

      <div className="flex min-h-0 flex-1 flex-col gap-3 xl:flex-row">
        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <DataCard
            fill
            toolbar={
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="text-sm font-semibold text-[#1A2238]">
                    Companies summary
                  </p>
                  <p className="text-xs text-[#6B7280]">
                    Header filters · sticky pagination
                  </p>
                </div>
                {!includeEmpty && totalCount === 0 ? (
                  <button
                    type="button"
                    className={btnSecondaryClass()}
                    onClick={() => setIncludeEmpty(true)}
                  >
                    Show empty companies
                  </button>
                ) : null}
              </div>
            }
          >
            <DataTable<DashboardInventoryRowDto>
              columns={columns}
              data={rows}
              getRowKey={(r) => r.companyId}
              loading={loading && rows.length === 0}
              emptyMessage="No active companies in this view. Clear filters or show empty companies."
              filters
              footerStickyToParent
              pagination={{
                totalCount,
                pageNumber: pageIndex + 1,
                pageSize,
                totalPages,
                onPageChange: (p) => {
                  if (p > pageIndex + 1) void goNext();
                  else if (p < pageIndex + 1) void goPrev();
                },
                onPageSizeChange: (size) => {
                  setPageSize(size);
                  setCursorStack([null]);
                  setPageIndex(0);
                },
              }}
            />
          </DataCard>
        </div>

        <NeedsAttentionPanel
          items={lowStock}
          showCompany={!isFiltered}
        />
      </div>
    </section>
  );
}

/* ─── Tenant Admin / Staff ────────────────────────────────────────────────── */

function TenantDashboard() {
  const displayName = sessionStore.getFormattedDisplayName();
  const sessionCompanyName = sessionStore.getCompanyName() ?? "Your company";

  const [data, setData] = useState<DashboardDto | null>(null);
  const [stockRows, setStockRows] = useState<StockItemDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadingKpis, setLoadingKpis] = useState(true);
  const [loadingStock, setLoadingStock] = useState(true);
  const [lowStockOnly, setLowStockOnly] = useState(true);
  const seededLowFilter = useRef(false);

  const [pageSize, setPageSize] = useState(10);
  const [pageIndex, setPageIndex] = useState(0);
  const [cursorStack, setCursorStack] = useState<Array<number | null>>([null]);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);

  const loadKpis = useCallback(async () => {
    setLoadingKpis(true);
    setLoadError(null);
    try {
      const dto = await getDashboard({ limit: 1 });
      setData(dto);
      if (!seededLowFilter.current) {
        seededLowFilter.current = true;
        setLowStockOnly((dto.totals?.lowStockProductCount ?? 0) > 0);
      }
    } catch {
      setLoadError(
        "We could not load the dashboard. Please refresh, or sign in again if this continues.",
      );
      setData(null);
    } finally {
      setLoadingKpis(false);
    }
  }, []);

  const loadStockPage = useCallback(
    async (afterId: number | null) => {
      setLoadingStock(true);
      setLoadError(null);
      try {
        const page = await listStock({
          afterId: afterId ?? undefined,
          limit: pageSize,
          lowStockOnly: lowStockOnly || undefined,
        });
        setStockRows(page.items ?? []);
        setTotalCount(page.totalCount ?? 0);
        setNextCursor(page.nextCursor ?? null);
        setHasMore(Boolean(page.hasMore));
      } catch {
        setLoadError(
          "Unable to load stock for your company. Check stock access or try Refresh.",
        );
        setStockRows([]);
      } finally {
        setLoadingStock(false);
      }
    },
    [pageSize, lowStockOnly],
  );

  useEffect(() => {
    void loadKpis();
  }, [loadKpis]);

  useEffect(() => {
    setCursorStack([null]);
    setPageIndex(0);
    void loadStockPage(null);
  }, [loadStockPage]);

  async function goNext() {
    if (!hasMore || nextCursor == null) return;
    const after = nextCursor;
    setCursorStack((s) => [...s, after]);
    setPageIndex((i) => i + 1);
    try {
      await loadStockPage(after);
    } catch {
      setLoadError("Unable to load next page.");
    }
  }

  async function goPrev() {
    if (pageIndex <= 0) return;
    const nextStack = cursorStack.slice(0, -1);
    const after = nextStack[nextStack.length - 1] ?? null;
    setCursorStack(nextStack);
    setPageIndex((i) => i - 1);
    try {
      await loadStockPage(after);
    } catch {
      setLoadError("Unable to load previous page.");
    }
  }

  function refreshAll() {
    setCursorStack([null]);
    setPageIndex(0);
    void loadKpis();
    void loadStockPage(null);
  }

  const totals = data?.totals;
  const lowCount = totals?.lowStockProductCount ?? 0;
  const companyLabel =
    data?.scopedCompanyName?.trim() || sessionCompanyName;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);
  const loading = loadingKpis || loadingStock;

  const columns = useMemo<DataTableColumn<StockItemDto>[]>(
    () => [
      {
        key: "productName",
        label: "Product",
        width: 220,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.productName}</span>
        ),
      },
      {
        key: "productCode",
        label: "SKU",
        width: 130,
        render: (r) => (
          <span className="font-mono text-xs text-[#1A2238]">{r.productCode}</span>
        ),
      },
      {
        key: "quantityOnHand",
        label: "Qty on hand",
        width: 110,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">{r.quantityOnHand}</span>
        ),
      },
      {
        key: "minimumStockLevel",
        label: "Min level",
        width: 100,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {r.minimumStockLevel}
          </span>
        ),
      },
      {
        key: "isLowStock",
        label: "Status",
        width: 100,
        filter: false,
        render: (r) =>
          r.isLowStock ? (
            <span className="font-semibold text-[#DC2626]">Low</span>
          ) : (
            <span className="font-semibold text-[#15803D]">OK</span>
          ),
      },
      {
        key: "actions",
        label: "Action",
        width: 100,
        filter: false,
        render: () => (
          <Link
            href="/stock"
            className="text-xs font-semibold text-[#2563EB] hover:underline"
          >
            Update
          </Link>
        ),
      },
    ],
    [],
  );

  return (
    <section className={pageCanvasFillClass("gap-3")} aria-label="Dashboard">
      <DashboardHeader
        displayName={displayName}
        greeting={companyLabel}
        lowCount={lowCount}
        loading={loading}
        onRefresh={refreshAll}
      >
        <div className="inline-flex h-9 max-w-full items-center gap-2 rounded-lg border border-white/15 bg-white/95 px-2.5 shadow-sm">
          <span className="text-[10px] font-bold uppercase tracking-wider text-[#64748B]">
            Company
          </span>
          <span className="truncate text-xs font-semibold text-[#1A2238]">
            {companyLabel}
          </span>
        </div>

        <button
          type="button"
          role="switch"
          aria-checked={lowStockOnly}
          title="Show only products at or below minimum stock"
          className={[
            "inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border px-2.5 text-[11px] font-semibold transition",
            lowStockOnly
              ? "border-[#FECACA] bg-[#DC2626] text-white shadow-sm"
              : "border-white/20 bg-white/10 text-[#E2E8F0] hover:bg-white/15",
          ].join(" ")}
          onClick={() => setLowStockOnly((v) => !v)}
        >
          <span
            aria-hidden
            className={[
              "h-1.5 w-1.5 rounded-full",
              lowStockOnly ? "bg-white" : "bg-[#94A3B8]",
            ].join(" ")}
          />
          Low only
        </button>
      </DashboardHeader>

      {loadError ? (
        <LoadError message={loadError} onRetry={refreshAll} />
      ) : null}

      <KpiStrip
        items={[
          { label: "Products", value: totals?.activeProductCount ?? 0 },
          { label: "Stock qty", value: totals?.totalQuantityOnHand ?? 0 },
          { label: "Low stock", value: lowCount, danger: lowCount > 0 },
          { label: "Vendors", value: totals?.activeVendorCount ?? 0 },
          {
            label: "Alerts",
            value: totals?.pendingLowStockAlerts ?? 0,
            danger: (totals?.pendingLowStockAlerts ?? 0) > 0,
          },
        ]}
      />

      <QuickNav
        links={[
          { href: "/stock", label: "Stock", highlight: lowCount > 0 },
          { href: "/sales", label: "Sales" },
          { href: "/products", label: "Products" },
          { href: "/vendors", label: "Vendors" },
          { href: "/categories", label: "Categories" },
          { href: "/brands", label: "Brands" },
        ]}
      />

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard
          fill
          toolbar={
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-semibold text-[#1A2238]">
                  {lowStockOnly ? "Needs attention — low stock" : "Your stock"}
                </p>
                <p className="text-xs text-[#6B7280]">
                  Same DataTable as Stock Management · header filters ·
                  pagination
                </p>
              </div>
              <Link href="/stock" className={btnPrimaryClass()}>
                Open Stock
              </Link>
            </div>
          }
        >
          <DataTable<StockItemDto>
            columns={columns}
            data={stockRows}
            getRowKey={(r) => r.productId}
            loading={loadingStock && stockRows.length === 0}
            emptyMessage={
              lowStockOnly
                ? "All good — no products below minimum. Turn off “Low only” to see all stock."
                : "No products yet. Add products first, then manage stock."
            }
            filters
            footerStickyToParent
            pagination={{
              totalCount,
              pageNumber: pageIndex + 1,
              pageSize,
              totalPages,
              onPageChange: (p) => {
                if (p > pageIndex + 1) void goNext();
                else if (p < pageIndex + 1) void goPrev();
              },
              onPageSizeChange: (size) => {
                setPageSize(size);
                setCursorStack([null]);
                setPageIndex(0);
              },
            }}
          />
        </DataCard>
      </div>
    </section>
  );
}

/* ─── Shared chrome ───────────────────────────────────────────────────────── */

function DashboardHeader({
  displayName,
  greeting,
  lowCount,
  loading,
  onRefresh,
  children,
}: {
  displayName: string;
  greeting: string;
  lowCount: number;
  loading: boolean;
  onRefresh: () => void;
  children?: ReactNode;
}) {
  return (
    <header className="shrink-0">
      <div className="rounded-xl border border-[#DBE3F0] bg-[#0B1B33] px-4 py-3 text-white sm:px-5">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <p className="text-xs text-[#93C5FD]">
              {displayName && displayName !== "User"
                ? `Hello, ${displayName}`
                : "Hello"}
              <span className="text-[#64748B]"> · </span>
              <span className="text-[#C5D0E0]">{greeting}</span>
            </p>
            <h1 className="text-xl font-semibold tracking-tight sm:text-2xl">
              Dashboard
            </h1>
            {lowCount > 0 ? (
              <p className="mt-1 text-xs font-semibold text-[#FCA5A5]">
                {lowCount} product{lowCount === 1 ? "" : "s"} need stock
                attention
              </p>
            ) : (
              <p className="mt-1 text-xs text-[#86EFAC]">
                Stock levels look healthy in this view
              </p>
            )}
          </div>

          <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 lg:max-w-xl lg:justify-end">
            {children}
            <button
              type="button"
              title="Reload latest numbers from the server"
              className="inline-flex h-9 shrink-0 items-center rounded-lg border border-white/20 bg-white/95 px-3 text-xs font-semibold text-[#1A2238] shadow-sm hover:bg-white disabled:opacity-60"
              disabled={loading}
              onClick={onRefresh}
            >
              {loading ? "…" : "Refresh"}
            </button>
          </div>
        </div>
      </div>
    </header>
  );
}

function LoadError({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <Alert tone="error" title="Something went wrong">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <span>{message}</span>
        <button
          type="button"
          className={btnPrimaryClass("shrink-0")}
          onClick={onRetry}
        >
          Try again
        </button>
      </div>
    </Alert>
  );
}

function KpiStrip({
  items,
}: {
  items: Array<{ label: string; value: number; danger?: boolean }>;
}) {
  const cols =
    items.length >= 6
      ? "xl:grid-cols-6"
      : items.length === 5
        ? "xl:grid-cols-5"
        : "xl:grid-cols-4";
  return (
    <div
      className={`grid shrink-0 grid-cols-2 gap-2 md:grid-cols-3 ${cols}`}
      aria-label="At a glance"
    >
      {items.map((k) => (
        <div
          key={k.label}
          className="rounded-lg border border-[#E5E7EB] bg-white px-3 py-2 shadow-sm"
        >
          <p className="text-[10px] font-semibold uppercase tracking-wide text-[#64748B]">
            {k.label}
          </p>
          <p
            className={[
              "text-lg font-semibold tabular-nums sm:text-xl",
              k.danger ? "text-[#DC2626]" : "text-[#1A2238]",
            ].join(" ")}
          >
            {k.value.toLocaleString()}
          </p>
        </div>
      ))}
    </div>
  );
}

function QuickNav({
  links,
}: {
  links: Array<{ href: string; label: string; highlight?: boolean }>;
}) {
  const { ready, hasForm } = useMyPermissions();
  const visible = ready
    ? links.filter((l) => hasForm(l.href))
    : links.filter((l) => l.href === "/dashboard");

  if (visible.length === 0) return null;

  return (
    <nav
      className="flex shrink-0 flex-wrap items-center gap-2 text-sm"
      aria-label="Quick actions"
    >
      <span className="text-xs font-semibold text-[#64748B]">Go to:</span>
      {visible.map((l) => (
        <Link
          key={l.href}
          href={l.href}
          className={[
            "rounded-md border px-2.5 py-1 text-xs font-semibold transition",
            l.highlight
              ? "border-[#FECACA] bg-[#FEF2F2] text-[#B91C1C]"
              : "border-[#E5E7EB] bg-white text-[#1A2238] hover:border-[#93C5FD]",
          ].join(" ")}
        >
          {l.label}
        </Link>
      ))}
    </nav>
  );
}

function NeedsAttentionPanel({
  items,
  showCompany,
}: {
  items: DashboardLowStockItemDto[];
  showCompany: boolean;
}) {
  return (
    <aside className="flex max-h-56 min-h-0 w-full shrink-0 flex-col overflow-hidden rounded-xl border border-[#E5E7EB] bg-white shadow-sm xl:max-h-none xl:w-72">
      <div className="shrink-0 border-b border-[#EEF2F7] px-3 py-2.5">
        <h2 className="text-sm font-semibold text-[#1A2238]">Needs attention</h2>
        <p className="text-[11px] text-[#6B7280]">Low stock products</p>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {items.length === 0 ? (
          <p className="px-3 py-8 text-center text-xs text-[#047857]">
            All good — nothing below minimum.
          </p>
        ) : (
          <ul className="divide-y divide-[#F1F5F9]">
            {items.map((item) => (
              <li
                key={`${item.companyId}-${item.productId}`}
                className="px-3 py-2.5"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate text-xs font-semibold text-[#1A2238]">
                      {item.productName}
                    </p>
                    <p className="truncate text-[10px] text-[#64748B]">
                      {showCompany ? `${item.companyName} · ` : ""}
                      Now {item.quantityOnHand} / Min {item.minimumStockLevel}
                    </p>
                  </div>
                  <Link
                    href="/stock"
                    className="shrink-0 text-[11px] font-semibold text-[#2563EB] hover:underline"
                  >
                    Update
                  </Link>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="shrink-0 border-t border-[#EEF2F7] p-2.5">
        <Link
          href="/stock"
          className="block rounded-lg bg-[#2563EB] px-3 py-2 text-center text-xs font-semibold text-white hover:bg-[#1D4ED8]"
        >
          Open Stock
        </Link>
      </div>
    </aside>
  );
}
