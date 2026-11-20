"use client";

/**
 * ==========================================================================================
 * WHAT: Forms listing + drawer CRUD (Company Module gold standard).
 * WHY: Form Name + Path catalog for permission-management navigation; soft-delete.
 * EXAMPLE: Route /settings/forms → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  createForm,
  deactivateForm,
  getForm,
  getFormActions,
  listActions,
  listForms,
  syncFormActions,
  updateForm,
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
  formCatalogSchema,
  type FormCatalogInput,
} from "@/schemas/rbac/create-form.schema";
import type { ActionDto, FormActionAssignmentDto, FormDto } from "@/types/rbac";

const FORM_PATH = "/settings/forms";

type DrawerMode = "view" | "create" | "edit";

export function FormsListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);
  const [rows, setRows] = useState<FormDto[]>([]);
  const [allActions, setAllActions] = useState<ActionDto[]>([]);
  const [assignedActionIds, setAssignedActionIds] = useState<number[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<FormDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<FormDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormCatalogInput>({
    resolver: zodResolver(formCatalogSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      name: "",
      path: "",
      isActive: true,
    },
  });

  async function refresh(filters: Record<string, string> = columnFilters) {
    const [data, actions] = await Promise.all([
      listForms({ shape: "list", filters }),
      listActions({ shape: "list" }),
    ]);
    setRows(data);
    setAllActions(actions.filter((a) => a.isActive));
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await refresh(columnFilters);
        if (!cancelled) setPageNumber(1);
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load forms. Login as Tenant Admin and retry.");
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
  const pageRows = useMemo<FormDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<FormDto>[]>(
    () => [
      {
        key: "name",
        label: "Form Name",
        width: 220,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.name}</span>,
      },
      {
        key: "path",
        label: "Form Path",
        width: 280,
        render: (r) => (
          <span className="font-mono text-[13px] text-[#2563EB]">{r.path}</span>
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

  function fillForm(row: FormDto | null) {
    reset({
      name: row?.name ?? "",
      path: row?.path ?? "",
      isActive: row?.isActive ?? true,
    });
  }

  async function loadAssignedActions(formId: number) {
    const assignments: FormActionAssignmentDto[] = await getFormActions(formId);
    setAssignedActionIds(
      assignments.filter((a) => a.isAssigned).map((a) => a.actionId),
    );
  }

  function toggleAssignedAction(actionId: number) {
    if (drawerMode === "view" || !canUpdate) return;
    setAssignedActionIds((prev) =>
      prev.includes(actionId)
        ? prev.filter((id) => id !== actionId)
        : [...prev, actionId],
    );
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setAssignedActionIds(
      allActions
        .filter((a) =>
          ["Create", "Read", "Update", "Delete", "View"].includes(a.name),
        )
        .map((a) => a.id),
    );
    setDrawerOpen(true);
  }

  function openView(row: FormDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setAssignedActionIds([]);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getForm(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
        await loadAssignedActions(row.id);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: FormDto) {
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setAssignedActionIds([]);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getForm(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
        await loadAssignedActions(row.id);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function requestDelete(row: FormDto) {
    if (!row.isActive) {
      setLoadError("Form is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateForm(pendingDelete.id);
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
        const created = await createForm({ name: values.name, path: values.path });
        if (assignedActionIds.length > 0 && canCreate) {
          await syncFormActions(created.id, { actionIds: assignedActionIds });
        }
      } else if (drawerMode === "edit" && editing) {
        await updateForm(editing.id, {
          name: values.name,
          path: values.path,
          isActive: values.isActive ?? true,
        });
        if (canUpdate) {
          await syncFormActions(editing.id, { actionIds: assignedActionIds });
        }
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create form failed. Path may already exist (active)."
          : "Update form failed. Path may already exist (active).",
      );
    }
  });

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add Form"
      : drawerMode === "view"
        ? "View Form"
        : "Edit Form";

  return (
    <SettingsPageChrome
      active="forms"
      title="Forms"
      breadcrumb={null}
      fill
      actions={
        <ClaimGate formPath={FORM_PATH} action="Create">
          <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
            <PlusIcon /> Add Form
          </button>
        </ClaimGate>
      }
    >
      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Forms" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <DataCard fill>
        <DataTable<FormDto>
          columns={columns}
          data={pageRows}
          getRowKey={(r) => r.id}
          emptyMessage="No forms found."
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
        title="Delete form?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.name}</span>
              {pendingDelete.path ? (
                <>
                  {" "}
                  (<span className="font-mono text-[13px]">{pendingDelete.path}</span>)
                </>
              ) : null}
              . It stays in history but will no longer appear in active listings.
            </>
          ) : null
        }
        confirmLabel="Delete form"
        cancelLabel="Keep form"
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
          drawerMode === "create" || drawerMode === "edit" ? "Save Form" : undefined
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
              Form Name
            </span>
            <input
              className={inputClass(Boolean(errors.name))}
              placeholder="Enter form name…"
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
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Form Path
            </span>
            <input
              className={inputClass(Boolean(errors.path))}
              placeholder="/settings/example"
              disabled={readOnly}
              aria-invalid={Boolean(errors.path)}
              {...register("path")}
            />
            {errors.path ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.path.message}
              </span>
            ) : null}
            <span className="text-[11px] text-[#8090A8]">
              Navigation route used for permission-management (must start with /).
            </span>
          </label>

          {drawerMode === "edit" ? (
            <label className="flex items-center gap-2 text-sm text-[#374151]">
              <input type="checkbox" className="h-4 w-4 accent-[#2563EB]" {...register("isActive")} />
              Active
            </label>
          ) : null}

          <div className="space-y-2">
            <span className={readOnly || !canUpdate ? fieldLabelMutedClass() : fieldLabelClass()}>
              Allowed actions
            </span>
            <p className="text-[11px] text-[#8090A8]">
              Catalog of actions available on this form before role grants.
            </p>
            <div className="flex flex-wrap gap-x-4 gap-y-2 rounded-lg border border-[#E8ECF2] bg-[#F8FAFC] p-3">
              {allActions.map((action) => (
                <label
                  key={action.id}
                  className="inline-flex items-center gap-1.5 text-[13px] text-[#374151]"
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-[#2563EB]"
                    checked={assignedActionIds.includes(action.id)}
                    disabled={readOnly || !canUpdate}
                    onChange={() => toggleAssignedAction(action.id)}
                  />
                  {action.name}
                </label>
              ))}
              {allActions.length === 0 ? (
                <span className="text-[12px] text-[#94A3B8]">No actions in catalog</span>
              ) : null}
            </div>
          </div>
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
