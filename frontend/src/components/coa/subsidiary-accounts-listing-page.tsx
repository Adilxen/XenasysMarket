"use client";

/**
 * ==========================================================================================
 * WHAT: Subsidiary Accounts listing + drawer CRUD (Company pattern; keyset pagination).
 * WHY: M1 Phase 3 — tenant leaves under Control; AccountNo+Code system-allocated (immutable).
 * EXAMPLE: Route /subsidiary-accounts → DataTable + SideDrawer
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createSubsidiaryAccount,
  deactivateSubsidiaryAccount,
  getSubsidiaryAccount,
  listControlAccounts,
  listSubsidiaryAccounts,
  updateSubsidiaryAccount,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { PageHeader } from "@/components/ui/page-header";
import { SideDrawer } from "@/components/ui/side-drawer";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useFormActions } from "@/lib/auth/use-my-permissions";
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
import type { ControlAccountDto, SubsidiaryAccountDto } from "@/types/api";

const FORM_PATH = "/subsidiary-accounts";

const subsidiaryFormSchema = z.object({
  controlAccountId: z.coerce
    .number({ required_error: "Control Account is required." })
    .int()
    .positive("Control Account is required."),
  subsidiaryAccountTitle: zNoHtmlString()
    .trim()
    .min(1, "Subsidiary Account Title is required.")
    .max(150, "Subsidiary Account Title must be at most 150 characters."),
});

type SubsidiaryFormInput = z.infer<typeof subsidiaryFormSchema>;

type DrawerMode = "view" | "create" | "edit";

function subsidiarySourceLabel(row: Pick<SubsidiaryAccountDto, "isSystemGenerated" | "controlKind">) {
  if (!row.isSystemGenerated) return "Manual";
  if (row.controlKind === "INVENTORY") return "Automation (Product)";
  if (row.controlKind === "ACCOUNTS_PAYABLE") return "Automation (Vendor)";
  return "Automation";
}

export function SubsidiaryAccountsListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);

  const [rows, setRows] = useState<SubsidiaryAccountDto[]>([]);
  const [controls, setControls] = useState<ControlAccountDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<SubsidiaryAccountDto | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<SubsidiaryAccountDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "subsidiaryAccountTitle",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<SubsidiaryFormInput>({
    resolver: zodResolver(subsidiaryFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      controlAccountId: 0,
      subsidiaryAccountTitle: "",
    },
  });

  async function loadPage(afterId: number | null, filters: Record<string, string> = columnFilters) {
    const page = await listSubsidiaryAccounts({
      afterId: afterId ?? undefined,
      limit: pageSize,
      filters,
      shape: "list",
    });
    setRows(page.items);
    setHasMore(page.hasMore);
    setNextCursor(page.nextCursor);
    setTotalCount(typeof page.totalCount === "number" ? page.totalCount : page.items.length);
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [_, controlPage] = await Promise.all([
          loadPage(null, columnFilters),
          listControlAccounts({ limit: 200, shape: "list" }),
        ]);
        if (!cancelled) {
          setControls(controlPage.items);
          setCursorStack([null]);
          setPageIndex(0);
        }
      } catch {
        if (!cancelled) {
          setLoadError(
            "Unable to load subsidiary accounts. Check coa.subsidiaries.read or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [columnFilters, pageSize]);

  const columns = useMemo<DataTableColumn<SubsidiaryAccountDto>[]>(
    () => [
      {
        key: "companyName",
        label: "Company",
        width: 160,
        render: (r) => (
          <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
        ),
      },
      {
        key: "accountNo",
        label: "Account No",
        width: 140,
        render: (r) => <span className="font-mono font-semibold text-[#1A2238]">{r.accountNo}</span>,
      },
      {
        key: "subsidiaryAccountCode",
        label: "Code",
        width: 120,
        render: (r) => <span className="font-mono text-[#4B5563]">{r.subsidiaryAccountCode}</span>,
      },
      {
        key: "subsidiaryAccountTitle",
        label: "Title",
        width: 220,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.subsidiaryAccountTitle}</span>
        ),
      },
      {
        key: "controlAccountTitle",
        label: "Control Account",
        width: 180,
        render: (r) => (
          <span className="text-[#4B5563]">
            {r.controlAccountTitle ?? "—"}
            {r.controlAccountCode ? (
              <span className="ml-1 font-mono text-[11px] text-[#8090A8]">
                ({r.controlAccountCode})
              </span>
            ) : null}
          </span>
        ),
      },
      {
        key: "isSystemGenerated",
        label: "Source",
        width: 170,
        filter: false,
        render: (r) => {
          const label = subsidiarySourceLabel(r);
          const isManual = label === "Manual";
          return (
            <span
              className={[
                "inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold",
                isManual ? "bg-[#EFF6FF] text-[#2563EB]" : "bg-[#F3F5F9] text-[#64748B]",
              ].join(" ")}
            >
              {label}
            </span>
          );
        },
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
              r.isActive ? "bg-[#EFF6FF] text-[#2563EB]" : "bg-[#F3F5F9] text-[#64748B]",
            ].join(" ")}
          >
            {r.isActive ? "Active" : "Inactive"}
          </span>
        ),
      },
    ],
    [],
  );

  function fillForm(row: SubsidiaryAccountDto | null) {
    reset({
      controlAccountId: row?.controlAccountId ?? controls[0]?.id ?? 0,
      subsidiaryAccountTitle: row?.subsidiaryAccountTitle ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: SubsidiaryAccountDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSubsidiaryAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list usable */
      }
    })();
  }

  function openEdit(row: SubsidiaryAccountDto) {
    if (!canUpdate) {
      setLoadError("Edit requires coa.subsidiaries.update.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSubsidiaryAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load subsidiary detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    const payload = {
      controlAccountId: values.controlAccountId,
      subsidiaryAccountTitle: values.subsidiaryAccountTitle.trim(),
    };
    try {
      if (drawerMode === "create") {
        await createSubsidiaryAccount(payload);
      } else if (editing) {
        await updateSubsidiaryAccount(editing.id, {
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
          ? "Create failed. Company context required."
          : "Update failed. Account Code / Account No are system formats and cannot change.",
      );
    }
  });

  function requestDelete(row: SubsidiaryAccountDto) {
    if (!canDelete) {
      setLoadError("Delete requires coa.subsidiaries.delete.");
      return;
    }
    if (row.isSystemGenerated) {
      setLoadError("System-generated subsidiary accounts cannot be deleted.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Subsidiary account is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateSubsidiaryAccount(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError("Delete failed. System-generated rows are protected.");
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
  const systemLocked = drawerMode === "edit" && editing?.isSystemGenerated === true;

  const drawerTitle =
    drawerMode === "create"
      ? "Add Subsidiary Account"
      : drawerMode === "view"
        ? "View Subsidiary Account"
        : "Edit Subsidiary Account";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Subsidiary Accounts"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
                <PlusIcon /> Add Subsidiary Account
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Subsidiary account action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<SubsidiaryAccountDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No subsidiary accounts for this company yet."
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
                showDelete={canDelete && !r.isSystemGenerated}
                deleteTitle={
                  r.isSystemGenerated
                    ? "System-generated — cannot delete"
                    : r.isActive
                      ? "Delete (deactivate)"
                      : "Already inactive"
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
        title="Delete subsidiary account?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">
                {pendingDelete.subsidiaryAccountTitle}
              </span>{" "}
              (<span className="font-mono text-[13px]">{pendingDelete.accountNo}</span>).
            </>
          ) : null
        }
        confirmLabel="Delete subsidiary"
        cancelLabel="Keep subsidiary"
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
          drawerMode === "create" || drawerMode === "edit" ? "Save Subsidiary Account" : undefined
        }
        onPrimary={readOnly ? undefined : () => void save()}
        primaryLoading={isSubmitting}
        wide
      >
        <form className="space-y-5" noValidate onSubmit={(e) => e.preventDefault()}>
          {formError ? (
            <Alert tone="error" title="Save failed" onDismiss={() => setFormError(null)}>
              {formError}
            </Alert>
          ) : null}

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Account No</span>
            <input
              className={inputClass(false)}
              value={
                drawerMode === "create"
                  ? "Auto — ACC-YYYY-##### (BRD)"
                  : (editing?.accountNo ?? "—")
              }
              readOnly
              disabled
            />
            <span className="text-[11px] text-[#8090A8]">
              System format — allocated on create, never updated.
            </span>
          </label>

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Account Code</span>
            <input
              className={inputClass(false)}
              value={
                drawerMode === "create"
                  ? "Auto — TenantCode-M##### (manual)"
                  : (editing?.subsidiaryAccountCode ?? "—")
              }
              readOnly
              disabled
            />
            <span className="text-[11px] text-[#8090A8]">
              System format — allocated with Account No, never updated.
            </span>
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly || systemLocked ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Control Account
            </span>
            <Controller
              name="controlAccountId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={controls.map((c) => ({
                    value: c.id,
                    label: `${c.code} — ${c.title}`,
                  }))}
                  value={field.value || null}
                  disabled={readOnly || systemLocked}
                  error={Boolean(errors.controlAccountId)}
                  placeholder="Select control account…"
                  searchPlaceholder="Search control accounts…"
                  emptyValues={[0, "", null, undefined]}
                  aria-invalid={Boolean(errors.controlAccountId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.controlAccountId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.controlAccountId.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Subsidiary Account Title
            </span>
            <input
              className={inputClass(Boolean(errors.subsidiaryAccountTitle))}
              placeholder="e.g. Manual adjustment ledger"
              disabled={readOnly}
              aria-invalid={Boolean(errors.subsidiaryAccountTitle)}
              {...register("subsidiaryAccountTitle")}
            />
            {errors.subsidiaryAccountTitle ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.subsidiaryAccountTitle.message}
              </span>
            ) : null}
          </label>

          {editing?.isSystemGenerated ? (
            <p className="text-[12px] text-[#8090A8]">
              {subsidiarySourceLabel(editing)} — Control and Code are locked.
            </p>
          ) : null}

          {drawerMode === "view" ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Source</span>
              <input
                className={inputClass(false)}
                value={editing ? subsidiarySourceLabel(editing) : "—"}
                readOnly
                disabled
              />
            </label>
          ) : null}
        </form>
      </SideDrawer>
    </section>
  );
}

function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
