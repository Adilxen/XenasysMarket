"use client";

/**
 * ==========================================================================================
 * WHAT: Brands listing + drawer CRUD (Vendor gold standard; no image).
 * WHY: BRD §4.4 — unique NameNormalized per tenant; soft-delete.
 * EXAMPLE: Route /brands → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createBrand,
  deactivateBrand,
  getBrand,
  listBrands,
  updateBrand,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
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
import type { BrandDto } from "@/types/catalog";

const FORM_PATH = "/brands";

const brandFormSchema = z.object({
  name: zNoHtmlString()
    .trim()
    .min(1, "Name is required.")
    .max(100, "Name must be at most 100 characters."),
  description: z
    .string()
    .trim()
    .max(500, "Description must be at most 500 characters.")
    .optional()
    .or(z.literal("")),
});

type BrandFormInput = z.infer<typeof brandFormSchema>;
type DrawerMode = "view" | "create" | "edit";

export function BrandListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } =
    useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const [rows, setRows] = useState<BrandDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<BrandDto | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<BrandDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "name",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<BrandFormInput>({
    resolver: zodResolver(brandFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: { name: "", description: "" },
  });

  async function loadPage(
    afterId: number | null,
    filters: Record<string, string> = columnFilters,
  ) {
    const page = await listBrands({
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
            "Unable to load brands. Check brands.read claim or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [columnFilters, pageSize]);

  const columns = useMemo<DataTableColumn<BrandDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 160,
              render: (r: BrandDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<BrandDto>,
          ]
        : []),
      {
        key: "name",
        label: "Name",
        width: 240,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.name}</span>
        ),
      },
      {
        key: "isActive",
        label: "Status",
        width: 100,
        filter: false,
        render: (r) => (
          <span
            className={[
              "inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold",
              r.isActive
                ? "bg-[#EFF6FF] text-[#2563EB]"
                : "bg-[#F3F5F9] text-[#64748B]",
            ].join(" ")}
          >
            {r.isActive ? "Active" : "Inactive"}
          </span>
        ),
      },
    ],
    [isSuperAdmin],
  );

  function fillForm(row: BrandDto | null) {
    reset({
      name: row?.name ?? "",
      description: row?.description ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: BrandDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getBrand(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: BrandDto) {
    if (!canUpdate) {
      setLoadError("Edit requires brands.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getBrand(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full brand detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    const payload = {
      name: values.name.trim(),
      description: values.description?.trim() || null,
    };
    try {
      if (drawerMode === "create") {
        await createBrand(payload);
      } else if (editing) {
        await updateBrand(editing.id, {
          ...payload,
          isActive: editing.isActive,
        });
      }
      setDrawerOpen(false);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create failed. Check unique brand name."
          : "Update failed. Check unique name and brands.update claim.",
      );
    }
  });

  function requestDelete(row: BrandDto) {
    if (!canDelete) {
      setLoadError("Delete requires brands.delete claim.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Brand is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateBrand(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError("Delete failed. Check brands.delete claim.");
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
  const drawerTitle =
    drawerMode === "create"
      ? "Add Brand"
      : drawerMode === "view"
        ? "View Brand"
        : "Edit Brand";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Brands"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button
                type="button"
                className={btnPrimaryClass()}
                onClick={openCreate}
              >
                <PlusIcon /> Add Brand
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Brand action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<BrandDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No brands registered yet."
            filters
            serverFilters
            columnFilters={columnFilters}
            onColumnFilterChange={onColumnFilterChange}
            footerStickyToParent
            actionsColumnWidth={128}
            renderActions={(r) => (
              <ListingRowActions
                onView={() => openView(r)}
                onEdit={() => openEdit(r)}
                onDelete={() => requestDelete(r)}
                showView={canView || canRead}
                showEdit={canUpdate}
                showDelete={canDelete}
                deleteTitle={
                  r.isActive ? "Delete (deactivate)" : "Already inactive"
                }
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
        title="Delete brand?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">
                {pendingDelete.name}
              </span>
              .
            </>
          ) : null
        }
        confirmLabel="Delete brand"
        cancelLabel="Keep brand"
        tone="danger"
        loading={deleting}
        onConfirm={() => void confirmDelete()}
        onCancel={() => {
          if (!deleting) setPendingDelete(null);
        }}
      />

      <SideDrawer
        open={drawerOpen}
        title={drawerTitle}
        onClose={() => setDrawerOpen(false)}
        cancelLabel={readOnly ? "Close" : "Cancel"}
        primaryLabel={
          drawerMode === "create" || drawerMode === "edit"
            ? "Save Brand"
            : undefined
        }
        onPrimary={readOnly ? undefined : () => void save()}
        primaryLoading={isSubmitting}
        wide
      >
        <form
          className="space-y-5"
          noValidate
          onSubmit={(e) => e.preventDefault()}
        >
          {formError ? (
            <Alert
              tone="error"
              title="Save failed"
              onDismiss={() => setFormError(null)}
            >
              {formError}
            </Alert>
          ) : null}

          {drawerMode !== "create" && editing?.companyName ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Company</span>
              <input
                className={inputClass(false)}
                value={editing.companyName}
                readOnly
                disabled
              />
            </label>
          ) : null}

          <label className="block space-y-1.5">
            <span
              className={
                readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)
              }
            >
              Name
            </span>
            <input
              className={inputClass(Boolean(errors.name))}
              placeholder="e.g. National"
              disabled={readOnly}
              aria-invalid={Boolean(errors.name)}
              {...register("name")}
            />
            {errors.name ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.name.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Description</span>
            <textarea
              className={inputClass(Boolean(errors.description))}
              rows={3}
              placeholder="Optional (max 500)"
              disabled={readOnly}
              {...register("description")}
            />
            {errors.description ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.description.message}
              </span>
            ) : null}
          </label>
        </form>
      </SideDrawer>
    </section>
  );
}

function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M12 5v14M5 12h14"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}
