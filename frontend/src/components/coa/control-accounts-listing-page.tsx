"use client";

/**
 * ==========================================================================================
 * WHAT: Control Accounts listing + right drawer CRUD (Company / Master pattern).
 * WHY: M1 Phase 2 — Inventory/AP automation nodes; Code server-allocated; SA write / tenant read.
 * EXAMPLE: Route /control-accounts → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createControlAccount,
  deactivateControlAccount,
  getControlAccount,
  listControlAccounts,
  listMasterAccounts,
  updateControlAccount,
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
import type { ControlAccountDto, MasterAccountDto } from "@/types/api";

const FORM_PATH = "/control-accounts";

const controlAccountFormSchema = z.object({
  masterAccountId: z.coerce
    .number({ required_error: "Master Account is required." })
    .int()
    .positive("Master Account is required."),
  title: zNoHtmlString()
    .trim()
    .min(1, "Control Account Title is required.")
    .max(100, "Control Account Title must be at most 100 characters."),
});

type ControlAccountFormInput = z.infer<typeof controlAccountFormSchema>;

type DrawerMode = "view" | "create" | "edit";

function isAutomationKind(kind: string) {
  return kind === "INVENTORY" || kind === "ACCOUNTS_PAYABLE";
}

function controlKindLabel(kind: string | null | undefined) {
  if (kind === "INVENTORY") return "Automation (Product)";
  if (kind === "ACCOUNTS_PAYABLE") return "Automation (Vendor)";
  return "Manual";
}

export function ControlAccountsListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);

  const [rows, setRows] = useState<ControlAccountDto[]>([]);
  const [masters, setMasters] = useState<MasterAccountDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<ControlAccountDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<ControlAccountDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "title",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ControlAccountFormInput>({
    resolver: zodResolver(controlAccountFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      masterAccountId: 0,
      title: "",
    },
  });

  async function refresh(filters: Record<string, string> = columnFilters) {
    const page = await listControlAccounts({ filters, limit: 100, shape: "list" });
    setRows(page.items);
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [_, masterPage] = await Promise.all([
          refresh(columnFilters),
          listMasterAccounts({ limit: 100, shape: "list" }),
        ]);
        if (!cancelled) {
          setMasters(masterPage.items);
          setPageNumber(1);
        }
      } catch {
        if (!cancelled) {
          setLoadError(
            "Unable to load control accounts. Check coa.controls.read claim or re-login after API restart.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refetch when filters change
  }, [columnFilters]);

  const filtered = rows;

  useEffect(() => {
    setPageNumber(1);
  }, [pageSize, rows.length]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize) || 1);
  const pageRows = useMemo<ControlAccountDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<ControlAccountDto>[]>(
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
        key: "code",
        label: "Code",
        width: 140,
        render: (r) => <span className="font-mono font-semibold text-[#1A2238]">{r.code}</span>,
      },
      {
        key: "title",
        label: "Title",
        width: 220,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.title}</span>,
      },
      {
        key: "masterAccountTitle",
        label: "Master Account",
        width: 160,
        render: (r) => (
          <span className="text-[#4B5563]">{r.masterAccountTitle ?? "—"}</span>
        ),
      },
      {
        key: "isActive",
        label: "Status",
        width: 110,
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

  function fillForm(row: ControlAccountDto | null) {
    reset({
      masterAccountId: row?.masterAccountId ?? masters[0]?.id ?? 0,
      title: row?.title ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: ControlAccountDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getControlAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row usable */
      }
    })();
  }

  function openEdit(row: ControlAccountDto) {
    if (!canUpdate) {
      setLoadError("Edit requires coa.controls.update.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getControlAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load control account detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    // Create always OTHER — Inventory/AP are company seed automation nodes (no form selection).
    const controlKind =
      drawerMode === "create" ? "OTHER" : (editing?.controlKind ?? "OTHER");
    const payload = {
      masterAccountId: values.masterAccountId,
      title: values.title.trim(),
      controlKind,
    };
    try {
      if (drawerMode === "create") {
        await createControlAccount(payload);
      } else if (editing) {
        await updateControlAccount(editing.id, {
          ...payload,
          isActive: editing.isActive,
        });
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create failed. Check company context and coa.controls.create."
          : "Update failed. Check coa.controls.update claim.",
      );
    }
  });

  function requestDelete(row: ControlAccountDto) {
    if (!canDelete) {
      setLoadError("Delete requires coa.controls.delete.");
      return;
    }
    if (isAutomationKind(row.controlKind)) {
      setLoadError("Inventory and Accounts Payable control accounts cannot be deleted.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Control account is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateControlAccount(pendingDelete.id);
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Delete failed. Automation kinds are protected.");
    } finally {
      setDeleting(false);
    }
  }

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add Control Account"
      : drawerMode === "view"
        ? "View Control Account"
        : "Edit Control Account";

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Control Accounts"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
                <PlusIcon /> Add Control Account
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Control account action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<ControlAccountDto>
            columns={columns}
            data={pageRows}
            getRowKey={(r) => r.id}
            emptyMessage="No control accounts yet."
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
                showDelete={canDelete && !isAutomationKind(r.controlKind)}
                deleteTitle={
                  isAutomationKind(r.controlKind)
                    ? "Protected automation node"
                    : r.isActive
                      ? "Delete (deactivate)"
                      : "Already inactive"
                }
              />
            )}
            pagination={{
              totalCount: filtered.length,
              pageNumber,
              pageSize,
              totalPages,
              onPageChange: setPageNumber,
              onPageSizeChange: (size) => {
                setPageSize(size);
                setPageNumber(1);
              },
            }}
          />
        </DataCard>
      </div>

      <ConfirmDialog
        open={pendingDelete != null}
        title="Delete control account?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.title}</span>
              {" "}
              (<span className="font-mono text-[13px]">{pendingDelete.code}</span>).
            </>
          ) : null
        }
        confirmLabel="Delete control account"
        cancelLabel="Keep control account"
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
          drawerMode === "create" || drawerMode === "edit" ? "Save Control Account" : undefined
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
            <span className={fieldLabelMutedClass()}>Control Account Code</span>
            <input
              className={inputClass(false)}
              value={
                drawerMode === "create"
                  ? "Auto-generated on save (e.g. AST-CTL-INV)"
                  : (editing?.code ?? "—")
              }
              readOnly
              disabled
            />
            <span className="text-[11px] text-[#8090A8]">
              Format: MasterPrefix-CTL-INV|AP|#### · unique & locked
            </span>
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Master Account
            </span>
            <Controller
              name="masterAccountId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={masters.map((m) => ({
                    value: m.id,
                    label: `${m.code} — ${m.title}`,
                  }))}
                  value={field.value || null}
                  disabled={readOnly}
                  error={Boolean(errors.masterAccountId)}
                  placeholder="Select master account…"
                  searchPlaceholder="Search master accounts…"
                  emptyValues={[0, "", null, undefined]}
                  aria-invalid={Boolean(errors.masterAccountId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.masterAccountId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.masterAccountId.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Control Account Title
            </span>
            <input
              className={inputClass(Boolean(errors.title))}
              placeholder="e.g. Inventory Control Account"
              disabled={readOnly}
              aria-invalid={Boolean(errors.title)}
              {...register("title")}
            />
            {errors.title ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.title.message}
              </span>
            ) : null}
          </label>

          {drawerMode === "view" ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Type</span>
              <input
                className={inputClass(false)}
                value={controlKindLabel(editing?.controlKind)}
                readOnly
                disabled
              />
              <span className="text-[11px] text-[#8090A8]">
                Automation (Product/Vendor) vs Manual — not editable from this form.
              </span>
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
