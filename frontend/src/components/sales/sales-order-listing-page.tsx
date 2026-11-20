"use client";

/**
 * ==========================================================================================
 * WHAT: Sales Orders listing + drawer (Phase-1 ledger; Brand gold + line editor).
 * WHY: BRD Trigger 4 volumes — post deducts stock; cancel restores QOH (T8.1).
 * EXAMPLE: Route /sales → DataTable + SideDrawer create with product lines
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  cancelSalesOrder,
  createSalesOrder,
  getSalesOrder,
  listProducts,
  listSalesOrders,
  updateSalesOrder,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { PageHeader } from "@/components/ui/page-header";
import { SearchableSelect } from "@/components/ui/searchable-select";
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
import type { ProductDto } from "@/types/api";
import type { SalesOrderDto, SalesOrderLineDto } from "@/types/sales";

const FORM_PATH = "/sales";

const headerSchema = z.object({
  customerName: zNoHtmlString()
    .trim()
    .max(150, "Customer Name must be at most 150 characters.")
    .optional()
    .or(z.literal("")),
  notes: z
    .string()
    .trim()
    .max(500, "Notes must be at most 500 characters.")
    .optional()
    .or(z.literal("")),
});

type HeaderFormInput = z.infer<typeof headerSchema>;
type DrawerMode = "view" | "create" | "edit";

type DraftLine = {
  key: string;
  productId: number;
  productLabel: string;
  quantity: number;
  unitPrice: number;
};

function money(n: number) {
  return n.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

function formatWhen(iso: string) {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

export function SalesOrderListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } =
    useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";
  const sessionCompanyId = Number(sessionStore.getTenantId() ?? 0);
  const sessionCompanyName = sessionStore.getCompanyName();

  const [rows, setRows] = useState<SalesOrderDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<SalesOrderDto | null>(null);
  const [draftLines, setDraftLines] = useState<DraftLine[]>([]);
  const [productOptions, setProductOptions] = useState<ProductDto[]>([]);
  const [pickProductId, setPickProductId] = useState<number>(0);
  const [pickQty, setPickQty] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<SalesOrderDto | null>(
    null,
  );
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "orderCode",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<HeaderFormInput>({
    resolver: zodResolver(headerSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: { customerName: "", notes: "" },
  });

  async function loadPage(
    afterId: number | null,
    filters: Record<string, string> = columnFilters,
  ) {
    const page = await listSalesOrders({
      afterId: afterId ?? undefined,
      limit: pageSize,
      filters,
      shape: "list",
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
        await loadPage(null, columnFilters);
        if (!cancelled) {
          setCursorStack([null]);
          setPageIndex(0);
        }
      } catch {
        if (!cancelled) {
          setLoadError(
            "Unable to load sales orders. Check sales.read claim or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [columnFilters, pageSize]);

  useEffect(() => {
    if (!drawerOpen || drawerMode !== "create") return;
    let cancelled = false;
    (async () => {
      try {
        if (!Number.isFinite(sessionCompanyId) || sessionCompanyId <= 0) {
          if (!cancelled) {
            setFormError(
              "Company context missing. Log in again selecting the target company.",
            );
          }
          return;
        }
        const page = await listProducts({
          companyId: sessionCompanyId,
          limit: 100,
          shape: "list",
        });
        if (!cancelled) setProductOptions(page.items);
      } catch {
        if (!cancelled) {
          setFormError("Unable to load products for line picker.");
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [drawerOpen, drawerMode, sessionCompanyId]);

  const columns = useMemo<DataTableColumn<SalesOrderDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 160,
              render: (r: SalesOrderDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<SalesOrderDto>,
          ]
        : []),
      {
        key: "orderCode",
        label: "Order Code",
        width: 120,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.orderCode}</span>
        ),
      },
      {
        key: "orderDate",
        label: "Order Date",
        width: 160,
        filter: false,
        render: (r) => (
          <span className="text-[#475569]">{formatWhen(r.orderDate)}</span>
        ),
      },
      {
        key: "customerName",
        label: "Customer",
        width: 160,
        render: (r) => (
          <span className="text-[#1A2238]">{r.customerName ?? "—"}</span>
        ),
      },
      {
        key: "grossAmount",
        label: "Gross",
        width: 110,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#1A2238]">
            {money(r.grossAmount)}
          </span>
        ),
      },
      {
        key: "status",
        label: "Status",
        width: 110,
        filter: false,
        render: (r) => (
          <span
            className={[
              "inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold",
              r.isActive && r.status === "Posted"
                ? "bg-[#EFF6FF] text-[#2563EB]"
                : "bg-[#F3F5F9] text-[#64748B]",
            ].join(" ")}
          >
            {r.status}
          </span>
        ),
      },
    ],
    [isSuperAdmin],
  );

  function fillForm(row: SalesOrderDto | null) {
    reset({
      customerName: row?.customerName ?? "",
      notes: row?.notes ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    setDraftLines([]);
    setPickProductId(0);
    setPickQty(1);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: SalesOrderDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSalesOrder(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: SalesOrderDto) {
    if (!canUpdate) {
      setLoadError("Edit requires sales.update claim.");
      return;
    }
    if (!row.isActive || row.status !== "Posted") {
      setLoadError("Only active posted orders can be edited.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSalesOrder(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full sales order detail.");
      }
    })();
  }

  function addDraftLine() {
    if (pickProductId <= 0 || pickQty <= 0) {
      setFormError("Select a product and quantity greater than 0.");
      return;
    }
    const product = productOptions.find((p) => p.id === pickProductId);
    if (!product) {
      setFormError("Selected product was not found.");
      return;
    }
    setFormError(null);
    setDraftLines((prev) => {
      const existing = prev.find((l) => l.productId === pickProductId);
      if (existing) {
        return prev.map((l) =>
          l.productId === pickProductId
            ? { ...l, quantity: l.quantity + pickQty }
            : l,
        );
      }
      return [
        ...prev,
        {
          key: `${pickProductId}-${Date.now()}`,
          productId: product.id,
          productLabel: `${product.productName} (${product.productCode})`,
          quantity: pickQty,
          unitPrice: Number(product.retailPrice) || 0,
        },
      ];
    });
    setPickProductId(0);
    setPickQty(1);
  }

  function removeDraftLine(key: string) {
    setDraftLines((prev) => prev.filter((l) => l.key !== key));
  }

  const draftGross = draftLines.reduce(
    (sum, l) => sum + l.quantity * l.unitPrice,
    0,
  );

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    try {
      if (drawerMode === "create") {
        if (draftLines.length === 0) {
          setFormError("Add at least one line before posting.");
          return;
        }
        if (!Number.isFinite(sessionCompanyId) || sessionCompanyId <= 0) {
          setFormError("Company context missing.");
          return;
        }
        await createSalesOrder({
          companyId: isSuperAdmin ? sessionCompanyId : undefined,
          customerName: values.customerName?.trim() || null,
          notes: values.notes?.trim() || null,
          lines: draftLines.map((l) => ({
            productId: l.productId,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
          })),
        });
      } else if (editing) {
        await updateSalesOrder(editing.id, {
          customerName: values.customerName?.trim() || null,
          notes: values.notes?.trim() || null,
        });
      }
      setDrawerOpen(false);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch (err) {
      const message =
        err && typeof err === "object" && "response" in err
          ? "Save failed. Check stock availability and sales claims."
          : "Save failed. Check stock availability and sales claims.";
      setFormError(message);
    }
  });

  function requestDelete(row: SalesOrderDto) {
    if (!canDelete) {
      setLoadError("Cancel requires sales.delete claim.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Order is already cancelled.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await cancelSalesOrder(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError("Cancel failed. Check sales.delete claim.");
    } finally {
      setDeleting(false);
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
  const headerOnlyEdit = drawerMode === "edit";
  const drawerTitle =
    drawerMode === "create"
      ? "Post Sale"
      : drawerMode === "view"
        ? "View Sales Order"
        : "Edit Sales Order";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);
  const displayLines: SalesOrderLineDto[] = editing?.lines ?? [];

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Sales Orders"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button
                type="button"
                className={btnPrimaryClass()}
                onClick={openCreate}
              >
                <PlusIcon /> Post Sale
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Sales action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<SalesOrderDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No sales orders posted yet."
            filters
            serverFilters
            columnFilters={columnFilters}
            onColumnFilterChange={onColumnFilterChange}
            footerStickyToParent
            actionsColumnWidth={128}
            renderActions={(r) => (
              <ListingRowActions
                onView={() => openView(r)}
                onEdit={
                  r.isActive && r.status === "Posted"
                    ? () => openEdit(r)
                    : undefined
                }
                onDelete={
                  r.isActive ? () => requestDelete(r) : undefined
                }
                showView={canView || canRead}
                showEdit={canUpdate && r.isActive && r.status === "Posted"}
                showDelete={canDelete && r.isActive}
                deleteTitle="Cancel sale (restore stock)"
              />
            )}
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

      <ConfirmDialog
        open={pendingDelete != null}
        title="Cancel sales order?"
        description={
          pendingDelete
            ? `Cancel ${pendingDelete.orderCode}? Stock quantities will be restored.`
            : ""
        }
        confirmLabel="Cancel order"
        loading={deleting}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setPendingDelete(null)}
      />

      <SideDrawer
        open={drawerOpen}
        title={drawerTitle}
        onClose={() => setDrawerOpen(false)}
        cancelLabel={drawerMode === "view" ? "Close" : "Cancel"}
        primaryLabel={
          drawerMode === "create"
            ? "Post sale"
            : drawerMode === "edit"
              ? "Save"
              : undefined
        }
        onPrimary={
          drawerMode === "view" ? undefined : () => void save()
        }
        primaryLoading={isSubmitting}
        primaryDisabled={isSubmitting}
      >
        <form className="space-y-4" onSubmit={(e) => e.preventDefault()}>
          {formError ? (
            <Alert tone="warning" title="Form" onDismiss={() => setFormError(null)}>
              {formError}
            </Alert>
          ) : null}

          {drawerMode === "create" && sessionCompanyName ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Company</span>
              <input
                className={inputClass(false)}
                value={sessionCompanyName}
                readOnly
                disabled
              />
            </label>
          ) : null}

          {drawerMode !== "create" && editing?.orderCode ? (
            <>
              <label className="block space-y-1.5">
                <span className={fieldLabelMutedClass()}>Order Code</span>
                <input
                  className={inputClass(false)}
                  value={editing.orderCode}
                  readOnly
                  disabled
                />
              </label>
              <label className="block space-y-1.5">
                <span className={fieldLabelMutedClass()}>Order Date</span>
                <input
                  className={inputClass(false)}
                  value={formatWhen(editing.orderDate)}
                  readOnly
                  disabled
                />
              </label>
            </>
          ) : null}

          <label className="block space-y-1.5">
            <span
              className={
                readOnly ? fieldLabelMutedClass() : fieldLabelClass(false)
              }
            >
              Customer Name
            </span>
            <input
              className={inputClass(Boolean(errors.customerName))}
              placeholder="Optional"
              disabled={readOnly}
              {...register("customerName")}
            />
            {errors.customerName ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.customerName.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span
              className={
                readOnly ? fieldLabelMutedClass() : fieldLabelClass(false)
              }
            >
              Notes
            </span>
            <textarea
              className={inputClass(Boolean(errors.notes))}
              rows={2}
              placeholder="Optional"
              disabled={readOnly}
              {...register("notes")}
            />
            {errors.notes ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.notes.message}
              </span>
            ) : null}
          </label>

          {drawerMode === "create" ? (
            <div className="space-y-3 rounded-lg border border-[#E2E8F0] p-3">
              <p className="text-sm font-semibold text-[#1A2238]">Lines</p>
              <div className="grid gap-2 sm:grid-cols-[1fr_88px_auto]">
                <SearchableSelect
                  options={productOptions.map((p) => ({
                    value: p.id,
                    label: `${p.productName} · ${p.productCode} (QOH ${p.quantityOnHand})`,
                  }))}
                  value={pickProductId || null}
                  placeholder="Select product"
                  searchPlaceholder="Search products…"
                  onChange={(v) =>
                    setPickProductId(typeof v === "number" ? v : 0)
                  }
                />
                <input
                  type="number"
                  min={1}
                  className={inputClass(false)}
                  value={pickQty}
                  onChange={(e) =>
                    setPickQty(Math.max(1, Number(e.target.value) || 1))
                  }
                  aria-label="Quantity"
                />
                <button
                  type="button"
                  className={btnPrimaryClass()}
                  onClick={addDraftLine}
                >
                  Add
                </button>
              </div>
              {draftLines.length === 0 ? (
                <p className="text-sm text-[#64748B]">No lines yet.</p>
              ) : (
                <ul className="space-y-2">
                  {draftLines.map((l) => (
                    <li
                      key={l.key}
                      className="flex items-start justify-between gap-2 text-sm"
                    >
                      <div>
                        <div className="font-medium text-[#1A2238]">
                          {l.productLabel}
                        </div>
                        <div className="text-[#64748B]">
                          {l.quantity} × {money(l.unitPrice)} ={" "}
                          {money(l.quantity * l.unitPrice)}
                        </div>
                      </div>
                      <button
                        type="button"
                        className="text-xs font-semibold text-[#DC2626]"
                        onClick={() => removeDraftLine(l.key)}
                      >
                        Remove
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <p className="text-sm font-semibold tabular-nums text-[#1A2238]">
                Gross: {money(draftGross)}
              </p>
            </div>
          ) : null}

          {(drawerMode === "view" || headerOnlyEdit) && displayLines.length > 0 ? (
            <div className="space-y-2 rounded-lg border border-[#E2E8F0] p-3">
              <p className="text-sm font-semibold text-[#1A2238]">Lines</p>
              <ul className="space-y-2">
                {displayLines.map((l) => (
                  <li key={l.id} className="text-sm">
                    <div className="font-medium text-[#1A2238]">
                      {l.productName} ({l.productCode})
                    </div>
                    <div className="text-[#64748B]">
                      {l.quantity} × {money(l.unitPrice)} ={" "}
                      {money(l.lineTotal)}
                    </div>
                  </li>
                ))}
              </ul>
              {editing ? (
                <p className="text-sm font-semibold tabular-nums text-[#1A2238]">
                  Gross: {money(editing.grossAmount)}
                </p>
              ) : null}
              {headerOnlyEdit ? (
                <p className="text-xs text-[#64748B]">
                  Lines are locked after post — cancel and re-post to change
                  products.
                </p>
              ) : null}
            </div>
          ) : null}
        </form>
      </SideDrawer>
    </section>
  );
}

function PlusIcon() {
  return (
    <svg
      className="mr-1.5 inline h-4 w-4"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      aria-hidden
    >
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}
