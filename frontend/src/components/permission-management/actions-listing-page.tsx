"use client";

/**
 * ==========================================================================================
 * WHAT: Actions listing + drawer CRUD (Company Module gold standard).
 * WHY: Name + IsActive soft-delete; claim gates deferred (T1.26).
 * EXAMPLE: Route /settings/actions → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  createAction,
  deactivateAction,
  getAction,
  listActions,
  updateAction,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import { SettingsPageChrome } from "@/components/settings/settings-module-shell";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { SideDrawer } from "@/components/ui/side-drawer";
import { useFormActions } from "@/lib/auth/use-my-permissions";
import { useServerColumnFilters } from "@/hooks/use-server-column-filters";
import {
  btnPrimaryClass,
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  inputClass,
} from "@/lib/forms/field-styles";
import {
  actionFormSchema,
  type ActionFormInput,
} from "@/schemas/rbac/create-action.schema";
import type { ActionDto } from "@/types/rbac";

const FORM_PATH = "/settings/actions";

type DrawerMode = "view" | "create" | "edit";

export function ActionsListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);
  const [rows, setRows] = useState<ActionDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<ActionDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<ActionDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ActionFormInput>({
    resolver: zodResolver(actionFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      name: "",
      isActive: true,
    },
  });

  async function refresh(filters: Record<string, string> = columnFilters) {
    const data = await listActions({ shape: "list", filters });
    setRows(data);
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await refresh(columnFilters);
        if (!cancelled) setPageNumber(1);
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load actions. Login as Tenant Admin and retry.");
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
  const pageRows = useMemo<ActionDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<ActionDto>[]>(
    () => [
      {
        key: "name",
        label: "Action Name",
        width: 240,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.name}</span>,
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

  function fillForm(row: ActionDto | null) {
    reset({
      name: row?.name ?? "",
      isActive: row?.isActive ?? true,
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: ActionDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getAction(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: ActionDto) {
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getAction(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function requestDelete(row: ActionDto) {
    if (!row.isActive) {
      setLoadError("Action is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateAction(pendingDelete.id);
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Delete failed. Retry or check API.");
    } finally {
      setDeleting(false);
    }
  }

  const save = handleSubmit(async (values) => {
    setFormError(null);
    try {
      if (drawerMode === "create") {
        await createAction({ name: values.name });
      } else if (drawerMode === "edit" && editing) {
        await updateAction(editing.id, {
          name: values.name,
          isActive: values.isActive ?? true,
        });
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create action failed. Name may already exist (active)."
          : "Update action failed. Name may already exist (active).",
      );
    }
  });

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add Action"
      : drawerMode === "view"
        ? "View Action"
        : "Edit Action";

  return (
    <SettingsPageChrome
      active="actions"
      title="Actions"
      breadcrumb={null}
      fill
      actions={
        <ClaimGate formPath={FORM_PATH} action="Create">
          <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
            <PlusIcon /> Add Action
          </button>
        </ClaimGate>
      }
    >
      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Actions" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <DataCard fill>
        <DataTable<ActionDto>
          columns={columns}
          data={pageRows}
          getRowKey={(r) => r.id}
          emptyMessage="No actions found."
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

      <ConfirmDialog
        open={pendingDelete != null}
        title="Delete action?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.name}</span>
              . It stays in history but will no longer appear in active listings.
            </>
          ) : null
        }
        confirmLabel="Delete action"
        cancelLabel="Keep action"
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
          drawerMode === "create" || drawerMode === "edit" ? "Save Action" : undefined
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
              Action Name
            </span>
            <input
              className={inputClass(Boolean(errors.name))}
              placeholder="Enter action name…"
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

          {drawerMode === "edit" ? (
            <label className="flex items-center gap-2 text-sm text-[#374151]">
              <input type="checkbox" className="h-4 w-4 accent-[#2563EB]" {...register("isActive")} />
              Active
            </label>
          ) : null}
        </form>
      </SideDrawer>
    </SettingsPageChrome>
  );
}

function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
