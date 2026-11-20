"use client";

/**
 * ==========================================================================================
 * WHAT: Roles listing + drawer CRUD with Form×Action grant matrix (T1.34).
 * WHY: Company gold-standard listing; RoleFormActions replaces PermissionCatalog checkboxes.
 * EXAMPLE: Route /settings/roles → DataTable + SideDrawer (view | create | edit + forms)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  createRole,
  deactivateRole,
  getRole,
  getRoleFormActions,
  listFormActionCatalog,
  listRoles,
  listTenants,
  syncRoleFormActions,
  updateRole,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import {
  SearchableCompanySelect,
  type CompanyOption,
} from "@/components/auth/searchable-company-select";
import {
  FormActionMatrix,
  selectionFromRoleForms,
  selectionToSyncItems,
  type FormActionSelection,
} from "@/components/permission-management/form-action-matrix";
import { SettingsPageChrome } from "@/components/settings/settings-module-shell";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { SideDrawer } from "@/components/ui/side-drawer";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { sessionStore } from "@/lib/auth/session";
import { useFormActions } from "@/lib/auth/use-my-permissions";
import {
  btnPrimaryClass,
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  inputClass,
} from "@/lib/forms/field-styles";
import { useServerColumnFilters } from "@/hooks/use-server-column-filters";
import {
  roleFormSchema,
  type RoleFormInput,
  normalizeDefaultRoute,
} from "@/schemas/rbac/create-role.schema";
import type { FormWithActionsDto, RoleDto } from "@/types/rbac";

const FORM_PATH = "/settings/roles";
const FALLBACK_ROUTE = "/dashboard";

type DrawerMode = "view" | "create" | "edit";

export function RolesListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);
  const canManagePerms = canUpdate;
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const [rows, setRows] = useState<RoleDto[]>([]);
  const [companies, setCompanies] = useState<CompanyOption[]>([]);
  const [catalog, setCatalog] = useState<FormWithActionsDto[]>([]);
  const [selectedActions, setSelectedActions] = useState<FormActionSelection>({});
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<RoleDto | null>(null);
  const [displayRoleCode, setDisplayRoleCode] = useState<string | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<RoleDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "roleCode",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<RoleFormInput>({
    resolver: zodResolver(roleFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      name: "",
      description: "",
      defaultRoute: FALLBACK_ROUTE,
      companyId: null,
      isActive: true,
    },
  });

  const formPathOptions = useMemo(() => {
    const paths = catalog
      .map((f) => f.path)
      .filter((p): p is string => Boolean(p?.startsWith("/")));
    const unique = Array.from(new Set([FALLBACK_ROUTE, ...paths])).sort();
    return unique;
  }, [catalog]);

  const showCompanyColumn = useMemo(() => {
    if (isSuperAdmin) return true;
    const ids = new Set(rows.map((r) => r.companyId ?? 0));
    return ids.size > 1;
  }, [isSuperAdmin, rows]);

  async function refresh(filters: Record<string, string> = columnFilters) {
    const [data, cat] = await Promise.all([
      listRoles({ shape: "list", filters }),
      listFormActionCatalog(),
    ]);
    setRows(data);
    setCatalog(cat);
    if (isSuperAdmin) {
      const tenants = await listTenants({ includeInactive: false });
      setCompanies(
        tenants.map((t) => ({ id: Number(t.id), name: t.name })),
      );
    }
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await refresh(columnFilters);
        if (!cancelled) setPageNumber(1);
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load roles. Check login session and retry.");
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
  const pageRows = useMemo<RoleDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<RoleDto>[]>(() => {
    const cols: DataTableColumn<RoleDto>[] = [];
    if (showCompanyColumn) {
      cols.push({
        key: "companyName",
        label: "Company",
        width: 200,
        render: (r) => (
          <span className="text-[#374151]">
            {r.companyName?.trim() || "—"}
          </span>
        ),
      });
    }
    cols.push(
      {
        key: "name",
        label: "Role Name",
        width: 180,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.name}</span>,
      },
      { key: "roleCode", label: "Role Code", width: 120 },
      {
        key: "description",
        label: "Description",
        width: 180,
        render: (r) => (
          <span className="text-[#6B7280]">{r.description?.trim() || "—"}</span>
        ),
      },
      {
        key: "defaultRoute",
        label: "Default Route",
        width: 150,
        filter: false,
        render: (r) => (
          <span className="font-mono text-[12px] text-[#374151]">
            {normalizeDefaultRoute(r.defaultRoute)}
          </span>
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
    );
    return cols;
  }, [showCompanyColumn]);

  function fillMeta(row: RoleDto | null) {
    setDisplayRoleCode(row?.roleCode ?? null);
    const jwtCompany = Number(sessionStore.getTenantId() || 0);
    reset({
      name: row?.name ?? "",
      description: row?.description ?? "",
      defaultRoute: normalizeDefaultRoute(row?.defaultRoute),
      companyId: row?.companyId ?? (jwtCompany > 0 ? jwtCompany : null),
      isActive: row?.isActive ?? true,
    });
  }

  async function loadRoleFormActions(roleId: number) {
    const grants = await getRoleFormActions(roleId);
    setSelectedActions(selectionFromRoleForms(grants.forms));
  }

  const permsLocked =
    drawerMode === "view" ||
    Boolean(editing?.isSystem) ||
    (drawerMode === "edit" && !canManagePerms);

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillMeta(null);
    setSelectedActions({});
    setDrawerOpen(true);
  }

  function openView(row: RoleDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillMeta(row);
    setSelectedActions({});
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getRole(row.id, "detail");
        setEditing(detail);
        fillMeta(detail);
        await loadRoleFormActions(row.id);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: RoleDto) {
    if (!canUpdate) {
      setLoadError("Edit requires roles.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillMeta(row);
    setSelectedActions({});
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getRole(row.id, "detail");
        setEditing(detail);
        fillMeta(detail);
        await loadRoleFormActions(row.id);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function requestDelete(row: RoleDto) {
    if (row.isSystem) {
      setLoadError("System roles cannot be deleted.");
      return;
    }
    if (!canDelete) {
      setLoadError("Delete requires roles.delete claim. Re-login after API restart.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Role is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateRole(pendingDelete.id);
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Delete failed. Check roles.delete claim and company scope.");
    } finally {
      setDeleting(false);
    }
  }

  const save = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const route = normalizeDefaultRoute(values.defaultRoute);
      if (drawerMode === "create") {
        if (isSuperAdmin && !(values.companyId && values.companyId > 0)) {
          setFormError("Select a company for this role.");
          return;
        }
        const created = await createRole({
          name: values.name,
          description: values.description || null,
          defaultRoute: route,
          ...(isSuperAdmin && values.companyId
            ? { company_id: values.companyId }
            : {}),
        });
        const items = selectionToSyncItems(selectedActions);
        if (items.length > 0) {
          await syncRoleFormActions(created.id, { items });
        }
      } else if (drawerMode === "edit" && editing) {
        await updateRole(editing.id, {
          name: values.name,
          description: values.description || null,
          defaultRoute: route,
          isActive: values.isActive ?? true,
        });
        if (canManagePerms && !editing.isSystem) {
          await syncRoleFormActions(editing.id, {
            items: selectionToSyncItems(selectedActions),
          });
        }
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create role failed. Check company / claims / form actions."
          : "Update failed. Check roles.update and Form×Action catalog.",
      );
    }
  });

  const isSystemEdit = drawerMode === "edit" && Boolean(editing?.isSystem);
  const metaReadOnly = drawerMode === "view" || isSystemEdit;
  const routeReadOnly = drawerMode === "view";
  const readOnlyPermissions = permsLocked;

  const drawerTitle =
    drawerMode === "create"
      ? "Add Role"
      : drawerMode === "view"
        ? "View Role"
        : "Edit Role";

  return (
    <SettingsPageChrome
      active="roles"
      title="Roles"
      breadcrumb={null}
      fill
      actions={
        <ClaimGate formPath={FORM_PATH} action="Create">
          <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
            <PlusIcon /> Add Role
          </button>
        </ClaimGate>
      }
    >
      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Roles action" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <DataCard fill>
        <DataTable<RoleDto>
          columns={columns}
          data={pageRows}
          getRowKey={(r) => r.id}
          emptyMessage="No roles found."
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
              showDelete={!r.isSystem && canDelete}
              deleteTitle={
                r.isSystem
                  ? "System role — cannot delete"
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

      <ConfirmDialog
        open={pendingDelete != null}
        title="Delete role?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.name}</span>
              {pendingDelete.roleCode ? (
                <>
                  {" "}
                  (<span className="font-mono text-[13px]">{pendingDelete.roleCode}</span>)
                </>
              ) : null}
              . The role stays in history but will no longer appear in active listings.
            </>
          ) : null
        }
        confirmLabel="Delete role"
        cancelLabel="Keep role"
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
        cancelLabel={drawerMode === "view" ? "Close" : "Cancel"}
        primaryLabel={
          drawerMode === "create" || drawerMode === "edit" ? "Save Role" : undefined
        }
        onPrimary={drawerMode === "view" ? undefined : () => void save()}
        primaryLoading={isSubmitting}
        wide
      >
        <form className="space-y-5" noValidate onSubmit={(e) => e.preventDefault()}>
          {formError ? (
            <Alert tone="error" title="Save failed" onDismiss={() => setFormError(null)}>
              {formError}
            </Alert>
          ) : null}

          {isSystemEdit ? (
            <Alert tone="warning" title="System role">
              Name / Form×Action locked. You can still set Default Route (post-login screen).
            </Alert>
          ) : null}

          {drawerMode === "create" && isSuperAdmin ? (
            <Controller
              name="companyId"
              control={control}
              rules={{ required: true }}
              render={({ field, fieldState }) => (
                <SearchableCompanySelect
                  label="Company"
                  required
                  options={companies}
                  value={field.value ?? null}
                  placeholder="Select company for this role"
                  error={
                    fieldState.error
                      ? "Company is required."
                      : formError?.includes("company")
                        ? formError
                        : undefined
                  }
                  onBlur={field.onBlur}
                  onChange={field.onChange}
                />
              )}
            />
          ) : null}

          {drawerMode !== "create" && showCompanyColumn ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Company</span>
              <input
                className={inputClass(false)}
                value={editing?.companyName?.trim() || "—"}
                readOnly
                disabled
              />
            </label>
          ) : null}

          <label className="block space-y-1.5">
            <span className={metaReadOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Role Name
            </span>
            <input
              className={inputClass(Boolean(errors.name))}
              placeholder="Enter role name…"
              disabled={metaReadOnly}
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
            <span className={fieldLabelMutedClass()}>Role Code</span>
            <input
              className={inputClass(false)}
              value={
                drawerMode === "create"
                  ? "Auto-generated on save (e.g. IN0012)"
                  : (displayRoleCode ?? "—")
              }
              readOnly
              disabled
            />
            <span className="text-[11px] text-[#8090A8]">
              {drawerMode === "create"
                ? "Format: 2-letter name prefix + 4-digit sequence · unique per company"
                : editing?.isSystem
                  ? "System role code · locked"
                  : "System-assigned · cannot change"}
            </span>
          </label>

          <label className="block space-y-1.5">
            <span className={metaReadOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Description
            </span>
            <textarea
              className={inputClass(Boolean(errors.description), "min-h-[96px] resize-y")}
              placeholder="Optional description…"
              disabled={metaReadOnly}
              aria-invalid={Boolean(errors.description)}
              {...register("description")}
            />
            {errors.description ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.description.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={routeReadOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Default Route
            </span>
            <Controller
              name="defaultRoute"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={formPathOptions.map((path) => ({
                    value: path,
                    label: path,
                  }))}
                  value={field.value || null}
                  disabled={routeReadOnly}
                  error={Boolean(errors.defaultRoute)}
                  placeholder="Select default route"
                  searchPlaceholder="Search routes…"
                  emptyValues={[null, undefined, ""]}
                  aria-invalid={Boolean(errors.defaultRoute)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "string" ? v : "")}
                />
              )}
            />
            {errors.defaultRoute ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.defaultRoute.message}
              </span>
            ) : (
              <span className="text-[11px] text-[#8090A8]">
                After login, user lands on this screen (must be granted on the role).
              </span>
            )}
          </label>

          {drawerMode === "edit" && !isSystemEdit ? (
            <label className="flex items-center gap-2 text-sm text-[#374151]">
              <input type="checkbox" className="h-4 w-4 accent-[#2563EB]" {...register("isActive")} />
              Active
            </label>
          ) : null}

          <div className="space-y-2">
            <span className={readOnlyPermissions ? fieldLabelMutedClass() : fieldLabelClass()}>
              Form × Action access
            </span>
            {editing?.isSystem ? (
              <Alert tone="warning" title="System role locked">
                System role Form×Action maps cannot be changed. View only.
              </Alert>
            ) : null}
            <FormActionMatrix
              catalog={catalog}
              selected={selectedActions}
              onChange={setSelectedActions}
              disabled={readOnlyPermissions}
            />
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
