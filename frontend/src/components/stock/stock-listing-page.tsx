"use client";

/**
 * ==========================================================================================
 * WHAT: Stock Management listing — qty on hand, min level, low-stock filter, update drawer.
 * WHY: M5 / BRD Stock — Update Stock; alerts when qty <= minimum (server outbox enqueue).
 * EXAMPLE: Route /stock → DataTable + SideDrawer (view | update)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useSearchParams } from "next/navigation";
import {
  getStockItem,
  listStock,
  listStockAlerts,
  listStockMovements,
  updateStock,
} from "@/lib/api/endpoints";
import { Alert } from "@/components/ui/alert";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { PageHeader } from "@/components/ui/page-header";
import { SideDrawer } from "@/components/ui/side-drawer";
import { useFormActions } from "@/lib/auth/use-my-permissions";
import { sessionStore } from "@/lib/auth/session";
import { useServerColumnFilters } from "@/hooks/use-server-column-filters";
import {
  btnPrimaryClass,
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  inputClass,
  pageCanvasFillClass,
} from "@/lib/forms/field-styles";
import { zNoHtmlString } from "@/lib/security/sanitize";
import type { StockAlertDto, StockItemDto, StockMovementDto } from "@/types/api";

const FORM_PATH = "/stock";

const stockFormSchema = z.object({
  quantityOnHand: z.coerce
    .number()
    .int("Quantity On Hand must be an integer.")
    .min(0, "Quantity On Hand must be >= 0."),
  minimumStockLevel: z.coerce
    .number()
    .int("Minimum Stock Level must be an integer.")
    .min(0, "Minimum Stock Level must be >= 0."),
  reason: zNoHtmlString()
    .trim()
    .max(250, "Reason must be at most 250 characters.")
    .optional()
    .or(z.literal("")),
});

type StockFormInput = z.infer<typeof stockFormSchema>;
type DrawerMode = "view" | "edit";

export function StockListingPage() {
  const { canUpdate, canView, canRead } = useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";
  const sessionCompanyId = Number(sessionStore.getTenantId() ?? 0);
  const searchParams = useSearchParams();

  const [rows, setRows] = useState<StockItemDto[]>([]);
  const [alerts, setAlerts] = useState<StockAlertDto[]>([]);
  const [movements, setMovements] = useState<StockMovementDto[]>([]);
  const [lowStockOnly, setLowStockOnly] = useState(
    () => searchParams.get("lowStockOnly") === "1",
  );
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("edit");
  const [editing, setEditing] = useState<StockItemDto | null>(null);
  const [pageSize, setPageSize] = useState(50);
  const [pageIndex, setPageIndex] = useState(0);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingAlertCount, setPendingAlertCount] = useState(0);

  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "productName",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<StockFormInput>({
    resolver: zodResolver(stockFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      quantityOnHand: 0,
      minimumStockLevel: 0,
      reason: "",
    },
  });

  async function loadAlertsBadge() {
    try {
      const page = await listStockAlerts({
        limit: 1,
        companyId:
          isSuperAdmin && sessionCompanyId > 0 ? sessionCompanyId : undefined,
      });
      setPendingAlertCount(
        typeof page.totalCount === "number" ? page.totalCount : page.items.length,
      );
      setAlerts(page.items);
    } catch {
      setPendingAlertCount(0);
    }
  }

  async function loadPage(
    afterId: number | null,
    filters: Record<string, string> = columnFilters,
    lowOnly = lowStockOnly,
  ) {
    const page = await listStock({
      afterId: afterId ?? undefined,
      limit: pageSize,
      filters,
      lowStockOnly: lowOnly || undefined,
      companyId:
        isSuperAdmin && sessionCompanyId > 0 ? sessionCompanyId : undefined,
    });
    setRows(page.items);
    setHasMore(page.hasMore);
    setNextCursor(page.nextCursor);
    setTotalCount(
      typeof page.totalCount === "number" ? page.totalCount : page.items.length,
    );
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await loadPage(null, columnFilters, lowStockOnly);
        await loadAlertsBadge();
        if (!cancelled) {
          setCursorStack([null]);
          setPageIndex(0);
        }
      } catch {
        if (!cancelled) {
          setLoadError(
            "Unable to load stock. Check stock.read claim or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [columnFilters, pageSize, lowStockOnly, sessionCompanyId]);

  const columns = useMemo<DataTableColumn<StockItemDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 140,
              render: (r: StockItemDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<StockItemDto>,
          ]
        : []),
      {
        key: "productName",
        label: "Product Name",
        width: 220,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.productName}</span>
        ),
      },
      {
        key: "productCode",
        label: "SKU",
        width: 150,
        render: (r) => (
          <span className="font-mono text-xs text-[#1A2238]">{r.productCode}</span>
        ),
      },
      {
        key: "quantityOnHand",
        label: "Qty On Hand",
        width: 110,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">{r.quantityOnHand}</span>
        ),
      },
      {
        key: "minimumStockLevel",
        label: "Min Level",
        width: 100,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {r.minimumStockLevel}
          </span>
        ),
      },
      {
        key: "unitOfMeasurement",
        label: "Unit",
        width: 80,
        filter: false,
        render: (r) => <span className="text-[#1A2238]">{r.unitOfMeasurement}</span>,
      },
      {
        key: "isLowStock",
        label: "Stock Status",
        width: 110,
        filter: false,
        render: (r) =>
          r.isLowStock ? (
            <span className="font-semibold text-[#DC2626]">Low</span>
          ) : (
            <span className="text-[#15803D]">OK</span>
          ),
      },
      {
        key: "actions",
        label: "Actions",
        width: 120,
        filter: false,
        render: (r) => (
          <ListingRowActions
            onView={
              canView || canRead
                ? () => void openDrawer(r, "view")
                : undefined
            }
            onEdit={canUpdate ? () => void openDrawer(r, "edit") : undefined}
            showView={canView || canRead}
            showEdit={canUpdate}
            showDelete={false}
            editTitle="Update Stock"
          />
        ),
      },
    ],
    [isSuperAdmin, canUpdate, canView, canRead],
  );

  async function openDrawer(row: StockItemDto, mode: DrawerMode) {
    if (mode === "edit" && !canUpdate) return;
    setFormError(null);
    setDrawerMode(mode);
    setEditing(row);
    try {
      const detail = await getStockItem(row.productId);
      reset({
        quantityOnHand: detail.quantityOnHand,
        minimumStockLevel: detail.minimumStockLevel,
        reason: "",
      });
      const mov = await listStockMovements(row.productId, { limit: 10 });
      setMovements(mov.items);
    } catch {
      reset({
        quantityOnHand: row.quantityOnHand,
        minimumStockLevel: row.minimumStockLevel,
        reason: "",
      });
      setMovements([]);
    }
    setDrawerOpen(true);
  }

  async function onSubmit(values: StockFormInput) {
    if (!editing || drawerMode !== "edit") return;
    setFormError(null);
    try {
      await updateStock(editing.productId, {
        quantityOnHand: values.quantityOnHand,
        minimumStockLevel: values.minimumStockLevel,
        reason: values.reason?.trim() || undefined,
      });
      setDrawerOpen(false);
      setEditing(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
      await loadAlertsBadge();
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Unable to update stock.";
      setFormError(message);
    }
  }

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

  const readOnly = drawerMode === "view";
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Stock Management"
          actions={
            <div className="flex flex-wrap items-center gap-2">
              {pendingAlertCount > 0 ? (
                <span className="rounded border border-[#FECACA] bg-[#FEF2F2] px-2 py-1 text-xs font-semibold text-[#DC2626]">
                  {pendingAlertCount} low-stock alert
                  {pendingAlertCount === 1 ? "" : "s"}
                </span>
              ) : null}
              <button
                type="button"
                className={
                  lowStockOnly
                    ? btnPrimaryClass()
                    : "rounded border border-[#D1D5DB] bg-white px-3 py-2 text-sm font-medium text-[#1A2238]"
                }
                onClick={() => setLowStockOnly((v) => !v)}
              >
                {lowStockOnly ? "Show all stock" : "Low stock only"}
              </button>
            </div>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Stock action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<StockItemDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.productId}
            emptyMessage={
              lowStockOnly
                ? "No low-stock products."
                : "No products available for stock."
            }
            filters
            serverFilters
            columnFilters={columnFilters}
            onColumnFilterChange={onColumnFilterChange}
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

      <SideDrawer
        open={drawerOpen}
        onClose={() => {
          setDrawerOpen(false);
          setEditing(null);
          setFormError(null);
        }}
        title={
          drawerMode === "view" ? "View Stock" : "Update Stock"
        }
        cancelLabel={readOnly ? "Close" : "Cancel"}
        primaryLabel={drawerMode === "edit" && canUpdate ? "Save Stock" : undefined}
        onPrimary={
          drawerMode === "edit" && canUpdate
            ? () => void handleSubmit(onSubmit)()
            : undefined
        }
        primaryLoading={isSubmitting}
        primaryDisabled={isSubmitting}
      >
        {formError ? (
          <Alert tone="error" title="Validation" onDismiss={() => setFormError(null)}>
            {formError}
          </Alert>
        ) : null}

        <form
          id="stock-form"
          className="flex flex-col gap-4"
          onSubmit={handleSubmit(onSubmit)}
        >
          <div>
            <p className={fieldLabelMutedClass()}>Product</p>
            <p className="font-semibold text-[#1A2238]">
              {editing?.productName ?? "—"}
            </p>
            <p className="font-mono text-xs text-[#6B7280]">
              {editing?.productCode}
            </p>
          </div>

          <div>
            <label className={fieldLabelClass()} htmlFor="quantityOnHand">
              Quantity On Hand
            </label>
            <input
              id="quantityOnHand"
              type="number"
              disabled={readOnly}
              className={inputClass(Boolean(errors.quantityOnHand))}
              {...register("quantityOnHand")}
            />
            {errors.quantityOnHand ? (
              <p className={fieldErrorClass()}>{errors.quantityOnHand.message}</p>
            ) : null}
          </div>

          <div>
            <label className={fieldLabelClass()} htmlFor="minimumStockLevel">
              Minimum Stock Level
            </label>
            <input
              id="minimumStockLevel"
              type="number"
              disabled={readOnly}
              className={inputClass(Boolean(errors.minimumStockLevel))}
              {...register("minimumStockLevel")}
            />
            {errors.minimumStockLevel ? (
              <p className={fieldErrorClass()}>
                {errors.minimumStockLevel.message}
              </p>
            ) : null}
          </div>

          {!readOnly ? (
            <div>
              <label className={fieldLabelClass()} htmlFor="reason">
                Reason (optional)
              </label>
              <input
                id="reason"
                type="text"
                className={inputClass(Boolean(errors.reason))}
                placeholder="e.g. Goods receipt"
                {...register("reason")}
              />
              {errors.reason ? (
                <p className={fieldErrorClass()}>{errors.reason.message}</p>
              ) : null}
            </div>
          ) : null}

          {movements.length > 0 ? (
            <div>
              <p className={fieldLabelMutedClass()}>Recent movements</p>
              <ul className="mt-1 max-h-40 space-y-1 overflow-auto text-sm">
                {movements.map((m) => (
                  <li
                    key={m.id}
                    className="flex justify-between gap-2 border-b border-[#F3F4F6] py-1 text-[#1A2238]"
                  >
                    <span className="tabular-nums">
                      {m.quantityDelta > 0 ? "+" : ""}
                      {m.quantityDelta} → {m.quantityAfter}
                    </span>
                    <span className="truncate text-[#6B7280]">
                      {m.reason || "—"}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {alerts.length > 0 && editing?.isLowStock ? (
            <p className="text-sm text-[#DC2626]">
              This product is at or below minimum stock level.
            </p>
          ) : null}
        </form>
      </SideDrawer>
    </section>
  );
}
