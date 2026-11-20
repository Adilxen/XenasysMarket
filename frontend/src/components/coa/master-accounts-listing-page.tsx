"use client";

/**
 * ==========================================================================================
 * WHAT: Chart of Accounts → Master Account listing + right drawer CRUD (Company pattern).
 * WHY: M1 Phase 1 — platform masters; Super Admin write; tenants read-only (BRD Task 1.1).
 * EXAMPLE: Route /chart-of-accounts → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createMasterAccount,
  deactivateMasterAccount,
  getMasterAccount,
  listMasterAccounts,
  updateMasterAccount,
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
import type { MasterAccountDto } from "@/types/api";

const FORM_PATH = "/chart-of-accounts";

const masterAccountFormSchema = z.object({
  code: z
    .string({ required_error: "Master Account Code is required." })
    .trim()
    .min(1, "Master Account Code is required.")
    .regex(/^[0-9]+$/, "Master Account Code must be numeric only.")
    .min(4, "Master Account Code must be at least 4 digits."),
  title: zNoHtmlString()
    .trim()
    .min(1, "Master Account Title is required.")
    .max(100, "Master Account Title must be at most 100 characters."),
  prefix: z
    .string({ required_error: "Prefix is required." })
    .trim()
    .min(1, "Prefix is required.")
    .max(5, "Prefix must be at most 5 characters.")
    .regex(/^[A-Za-z0-9]+$/, "Prefix must be alphanumeric."),
});

type MasterAccountFormInput = z.infer<typeof masterAccountFormSchema>;

type DrawerMode = "view" | "create" | "edit";

export function MasterAccountsListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);

  const [rows, setRows] = useState<MasterAccountDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<MasterAccountDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<MasterAccountDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "title",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<MasterAccountFormInput>({
    resolver: zodResolver(masterAccountFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      code: "",
      title: "",
      prefix: "",
    },
  });

  async function refresh(filters: Record<string, string> = columnFilters) {
    const page = await listMasterAccounts({ filters, limit: 100, shape: "list" });
    setRows(page.items);
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await refresh(columnFilters);
        if (!cancelled) setPageNumber(1);
      } catch {
        if (!cancelled) {
          setLoadError(
            "Unable to load master accounts. Check coa.masters.read claim or re-login after API restart.",
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
  const pageRows = useMemo<MasterAccountDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<MasterAccountDto>[]>(
    () => [
      {
        key: "code",
        label: "Code",
        width: 120,
        render: (r) => <span className="font-mono font-semibold text-[#1A2238]">{r.code}</span>,
      },
      {
        key: "title",
        label: "Title",
        width: 220,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.title}</span>,
      },
      {
        key: "prefix",
        label: "Prefix",
        width: 100,
        render: (r) => <span className="font-mono text-[#4B5563]">{r.prefix}</span>,
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

  function fillForm(row: MasterAccountDto | null) {
    reset({
      code: row?.code ?? "",
      title: row?.title ?? "",
      prefix: row?.prefix ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: MasterAccountDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getMasterAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: MasterAccountDto) {
    if (!canUpdate) {
      setLoadError("Edit requires coa.masters.update (Super Admin).");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getMasterAccount(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load master account detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    const payload = {
      code: values.code.trim(),
      title: values.title.trim(),
      prefix: values.prefix.trim().toUpperCase(),
    };
    try {
      if (drawerMode === "create") {
        await createMasterAccount(payload);
      } else if (editing) {
        await updateMasterAccount(editing.id, {
          ...payload,
          isActive: editing.isActive,
        });
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create failed. Super Admin only; code and prefix must be unique."
          : "Update failed. Check coa.masters.update claim.",
      );
    }
  });

  function requestDelete(row: MasterAccountDto) {
    if (!canDelete) {
      setLoadError("Delete requires coa.masters.delete (Super Admin). Re-login after API restart.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Master account is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateMasterAccount(pendingDelete.id);
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Delete failed. Super Admin only.");
    } finally {
      setDeleting(false);
    }
  }

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add Master Account"
      : drawerMode === "view"
        ? "View Master Account"
        : "Edit Master Account";

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Chart of Accounts"
          subtitle="Master accounts (platform-shared)"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
                <PlusIcon /> Add Master Account
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Master account action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<MasterAccountDto>
            columns={columns}
            data={pageRows}
            getRowKey={(r) => r.id}
            emptyMessage="No master accounts yet."
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
                deleteTitle={r.isActive ? "Delete (deactivate)" : "Already inactive"}
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
        title="Delete master account?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.title}</span>
              {" "}
              (<span className="font-mono text-[13px]">{pendingDelete.code}</span>). Control
              accounts under this master stay linked; the row disappears from active listings.
            </>
          ) : null
        }
        confirmLabel="Delete master account"
        cancelLabel="Keep master account"
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
          drawerMode === "create"
            ? "Save Master Account"
            : drawerMode === "edit"
              ? "Save Master Account"
              : undefined
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
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Master Account Code
            </span>
            <input
              className={inputClass(Boolean(errors.code))}
              placeholder="e.g. 1000"
              disabled={readOnly}
              aria-invalid={Boolean(errors.code)}
              {...register("code")}
            />
            {errors.code ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.code.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Master Account Title
            </span>
            <input
              className={inputClass(Boolean(errors.title))}
              placeholder="e.g. Assets"
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

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Prefix
            </span>
            <input
              className={inputClass(Boolean(errors.prefix))}
              placeholder="e.g. AST"
              disabled={readOnly}
              aria-invalid={Boolean(errors.prefix)}
              {...register("prefix")}
            />
            {errors.prefix ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.prefix.message}
              </span>
            ) : null}
            <span className="text-[11px] text-[#8090A8]">
              Alphanumeric, max 5 · stored uppercase · must be unique
            </span>
          </label>
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
