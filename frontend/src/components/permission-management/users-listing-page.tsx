"use client";

/**
 * ==========================================================================================
 * WHAT: Users listing + drawer CRUD (Company Module gold standard) + multi-role (T1.38).
 * WHY: JWT-company memberships; RoleIds[] → UserRoles; soft-deactivate.
 * EXAMPLE: Route /settings/users → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import axios from "axios";
import {
  addTenantUserMembership,
  createTenantUser,
  deactivateTenantUserMembership,
  getTenantUser,
  listRoles,
  listTenants,
  listTenantUsers,
  updateTenantUser,
  updateTenantUserMembership,
} from "@/lib/api/endpoints";
import { ClaimGate } from "@/components/auth/claim-gate";
import {
  SearchableCompanySelect,
  type CompanyOption,
} from "@/components/auth/searchable-company-select";
import { SettingsPageChrome } from "@/components/settings/settings-module-shell";
import { Alert } from "@/components/ui/alert";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataCard } from "@/components/ui/data-card";
import DataTable, { type DataTableColumn } from "@/components/ui/data-table";
import { ListingRowActions } from "@/components/ui/listing-row-actions";
import { SideDrawer } from "@/components/ui/side-drawer";
import { sessionStore } from "@/lib/auth/session";
import { useFormActions } from "@/lib/auth/use-my-permissions";
import {
  btnPrimaryClass,
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  inputClass,
} from "@/lib/forms/field-styles";
import { mapApiErrorsToForm } from "@/lib/forms/map-api-errors";
import { useServerColumnFilters } from "@/hooks/use-server-column-filters";
import {
  userFormSchema,
  type UserFormInput,
} from "@/schemas/rbac/create-user.schema";
import type { RoleDto, TenantUserDto } from "@/types/rbac";

const FORM_PATH = "/settings/users";

type DrawerMode = "view" | "create" | "edit";

export function UsersListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const [rows, setRows] = useState<TenantUserDto[]>([]);
  const [allRoles, setAllRoles] = useState<RoleDto[]>([]);
  const [companies, setCompanies] = useState<CompanyOption[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<TenantUserDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<TenantUserDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [membershipBusy, setMembershipBusy] = useState(false);
  const [addCompanyId, setAddCompanyId] = useState<number | null>(null);
  const [addRoleIds, setAddRoleIds] = useState<number[]>([]);
  const [membershipDrafts, setMembershipDrafts] = useState<
    Record<number, { roleIds: number[]; membershipIsActive: boolean }>
  >({});
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "userName",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    watch,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UserFormInput>({
    resolver: zodResolver(userFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      mode: "create",
      userName: "",
      password: "",
      email: "",
      companyId: null,
      roleIds: [],
      membershipIsActive: true,
    },
  });

  const watchedCompanyId = watch("companyId");

  async function refresh(filters: Record<string, string> = columnFilters) {
    const [u, r] = await Promise.all([
      listTenantUsers({ includeInactive: true, filters }),
      listRoles({ shape: "list" }),
    ]);
    setRows(u);
    setAllRoles(r.filter((x) => x.roleCode !== "SUPER_ADMIN" && x.isActive));
    if (isSuperAdmin) {
      const tenants = await listTenants({ includeInactive: false });
      setCompanies(tenants.map((t) => ({ id: Number(t.id), name: t.name })));
    }
  }

  const roles = useMemo(() => {
    const isSharedTenantRole = (r: RoleDto) =>
      (r.companyId == null || r.companyId === 0) && r.roleCode === "TENANT_ADMIN";

    if (!isSuperAdmin) {
      const jwtCompany = Number(sessionStore.getTenantId() || 0);
      return allRoles.filter(
        (r) => isSharedTenantRole(r) || Number(r.companyId) === jwtCompany,
      );
    }
    if (drawerMode === "create") {
      if (!(watchedCompanyId && watchedCompanyId > 0)) return [];
      return allRoles.filter(
        (r) => isSharedTenantRole(r) || Number(r.companyId) === watchedCompanyId,
      );
    }
    // Edit/view Super Admin: roles filtered per membership in Companies panel.
    return allRoles;
  }, [allRoles, drawerMode, isSuperAdmin, watchedCompanyId]);

  const addCompanyRoles = useMemo(() => {
    const isSharedTenantRole = (r: RoleDto) =>
      (r.companyId == null || r.companyId === 0) && r.roleCode === "TENANT_ADMIN";
    if (!(addCompanyId && addCompanyId > 0)) return [];
    return allRoles.filter(
      (r) => isSharedTenantRole(r) || Number(r.companyId) === addCompanyId,
    );
  }, [addCompanyId, allRoles]);

  const assignedCompanyIds = useMemo(() => {
    const list = editing?.companies ?? [];
    return new Set(list.map((c) => Number(c.companyId)));
  }, [editing?.companies]);

  const companiesAvailableToAdd = useMemo(
    () => companies.filter((c) => !assignedCompanyIds.has(c.id)),
    [companies, assignedCompanyIds],
  );

  function rolesForCompany(companyId: number): RoleDto[] {
    const isSharedTenantRole = (r: RoleDto) =>
      (r.companyId == null || r.companyId === 0) && r.roleCode === "TENANT_ADMIN";
    return allRoles.filter(
      (r) => isSharedTenantRole(r) || Number(r.companyId) === companyId,
    );
  }

  function syncMembershipDrafts(detail: TenantUserDto | null) {
    const next: Record<number, { roleIds: number[]; membershipIsActive: boolean }> =
      {};
    for (const c of detail?.companies ?? []) {
      next[Number(c.companyId)] = {
        roleIds: (c.roles ?? []).map((r) => Number(r.roleId)),
        membershipIsActive: c.membershipIsActive,
      };
    }
    setMembershipDrafts(next);
    setAddCompanyId(null);
    setAddRoleIds([]);
  }

  useEffect(() => {
    if (drawerMode !== "create" || !isSuperAdmin) return;
    setValue("roleIds", []);
  }, [watchedCompanyId, drawerMode, isSuperAdmin, setValue]);

  useEffect(() => {
    setAddRoleIds([]);
  }, [addCompanyId]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        await refresh(columnFilters);
        if (!cancelled) setPageNumber(1);
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load users. Check login session and retry.");
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
  const pageRows = useMemo<TenantUserDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<TenantUserDto>[]>(
    () => [
      {
        key: "userName",
        label: "User Name",
        width: 160,
        render: (u) => (
          <span className="font-semibold text-[#1A2238]">{u.userName}</span>
        ),
      },
      {
        key: "email",
        label: "Email",
        width: 200,
        render: (u) => (
          <span className="text-[#6B7280]">{u.email?.trim() || "—"}</span>
        ),
      },
      {
        key: "companies",
        filterKey: "companyName",
        label: "Company",
        width: 220,
        render: (u) => {
          const names =
            u.companies?.length
              ? u.companies.map((c) => c.companyName)
              : u.companyName
                ? [u.companyName]
                : [];
          return (
            <span className="text-[#374151]">
              {names.length > 0 ? names.join(", ") : "—"}
            </span>
          );
        },
      },
      {
        key: "roles",
        filterKey: "roleName",
        label: "Roles",
        width: 220,
        render: (u) => {
          const labels =
            u.roles?.length
              ? u.roles.map((r) => r.roleName)
              : u.roleName
                ? [u.roleName]
                : [];
          return (
            <span className="text-[#374151]">
              {labels.length > 0 ? labels.join(", ") : "—"}
            </span>
          );
        },
      },
      {
        key: "membershipIsActive",
        label: "Status",
        width: 110,
        filter: false,
        render: (u) => (
          <span
            className={[
              "inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold",
              u.membershipIsActive && u.isActive
                ? "bg-[#EFF6FF] text-[#2563EB]"
                : "bg-[#F3F5F9] text-[#64748B]",
            ].join(" ")}
          >
            {u.membershipIsActive && u.isActive ? "Active" : "Inactive"}
          </span>
        ),
      },
    ],
    [],
  );

  function fillForm(mode: DrawerMode, row: TenantUserDto | null) {
    const roleIds =
      row?.roles?.map((r) => r.roleId) ??
      (row?.roleId ? [row.roleId] : []);
    const jwtCompany = Number(sessionStore.getTenantId() || 0);
    reset({
      mode,
      userName: row?.userName ?? "",
      password: "",
      email: row?.email ?? "",
      companyId:
        row?.companyId ?? (jwtCompany > 0 ? jwtCompany : null),
      roleIds,
      membershipIsActive: row?.membershipIsActive ?? true,
    });
  }

  function openCreate() {
    if (!canCreate) {
      setLoadError("Create requires Create action on Users.");
      return;
    }
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm("create", null);
    syncMembershipDrafts(null);
    setDrawerOpen(true);
  }

  function openView(row: TenantUserDto) {
    if (!canView && !canRead) {
      setLoadError("View requires Read/View action on Users.");
      return;
    }
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm("view", row);
    syncMembershipDrafts(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getTenantUser(row.id);
        setEditing(detail);
        fillForm("view", detail);
        syncMembershipDrafts(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: TenantUserDto) {
    if (!canUpdate) {
      setLoadError("Edit requires Update action on Users.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm("edit", row);
    syncMembershipDrafts(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getTenantUser(row.id);
        setEditing(detail);
        fillForm("edit", detail);
        syncMembershipDrafts(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function requestDelete(row: TenantUserDto) {
    if (!canDelete) {
      setLoadError("Delete requires Delete action on Users.");
      return;
    }
    if (!row.membershipIsActive) {
      setLoadError("User membership is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      const companyId = Number(pendingDelete.companyId || 0);
      await deactivateTenantUserMembership(
        pendingDelete.id,
        companyId > 0 ? companyId : undefined,
      );
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Deactivate failed. Retry or check API.");
    } finally {
      setDeleting(false);
    }
  }

  async function applyMembership(companyId: number) {
    if (!editing || drawerMode !== "edit") return;
    const draft = membershipDrafts[companyId];
    if (!draft || draft.roleIds.length === 0) {
      setFormError("Select at least one role for this company.");
      return;
    }
    setMembershipBusy(true);
    setFormError(null);
    try {
      const detail = await updateTenantUserMembership(editing.id, companyId, {
        roleIds: draft.roleIds,
        membershipIsActive: draft.membershipIsActive,
      });
      setEditing(detail);
      syncMembershipDrafts(detail);
      await refresh();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.data) {
        const fields = (err.response.data as { errors?: Record<string, string[]> })
          ?.errors;
        const first =
          fields &&
          Object.values(fields)
            .flat()
            .find((m) => typeof m === "string" && m.length > 0);
        if (first) {
          setFormError(first);
          return;
        }
      }
      setFormError("Could not update company membership.");
    } finally {
      setMembershipBusy(false);
    }
  }

  async function addMembership() {
    if (!editing || drawerMode !== "edit") return;
    if (!(addCompanyId && addCompanyId > 0)) {
      setFormError("Select a company to add.");
      return;
    }
    if (addRoleIds.length === 0) {
      setFormError("Select at least one role for the new company.");
      return;
    }
    setMembershipBusy(true);
    setFormError(null);
    try {
      const detail = await addTenantUserMembership(editing.id, {
        company_id: addCompanyId,
        roleIds: addRoleIds,
      });
      setEditing(detail);
      syncMembershipDrafts(detail);
      await refresh();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.data) {
        const fields = (err.response.data as { errors?: Record<string, string[]> })
          ?.errors;
        const first =
          fields &&
          Object.values(fields)
            .flat()
            .find((m) => typeof m === "string" && m.length > 0);
        if (first) {
          setFormError(first);
          return;
        }
      }
      setFormError("Could not add company membership.");
    } finally {
      setMembershipBusy(false);
    }
  }

  async function deactivateMembershipRow(companyId: number) {
    if (!editing || drawerMode !== "edit") return;
    setMembershipBusy(true);
    setFormError(null);
    try {
      await deactivateTenantUserMembership(editing.id, companyId);
      const detail = await getTenantUser(editing.id);
      setEditing(detail);
      syncMembershipDrafts(detail);
      await refresh();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.data) {
        const fields = (err.response.data as { errors?: Record<string, string[]> })
          ?.errors;
        const first =
          fields &&
          Object.values(fields)
            .flat()
            .find((m) => typeof m === "string" && m.length > 0);
        if (first) {
          setFormError(first);
          return;
        }
      }
      setFormError("Could not deactivate company membership.");
    } finally {
      setMembershipBusy(false);
    }
  }

  const save = handleSubmit(async (values) => {
    setFormError(null);
    try {
      if (drawerMode === "create") {
        if (isSuperAdmin && !(values.companyId && values.companyId > 0)) {
          setFormError("Select a company for this user.");
          return;
        }
        await createTenantUser({
          userName: values.userName,
          password: values.password ?? "",
          email: values.email || null,
          roleIds: values.roleIds,
          ...(isSuperAdmin && values.companyId
            ? { company_id: values.companyId }
            : {}),
        });
      } else if (drawerMode === "edit" && editing) {
        const pwd = values.password?.trim() ?? "";
        if (isSuperAdmin) {
          // Profile-only; company roles saved per-membership below.
          await updateTenantUser(editing.id, {
            email: values.email || null,
            password: pwd.length > 0 ? pwd : null,
          });
        } else {
          if (!values.roleIds || values.roleIds.length < 1) {
            setFormError("Select at least one role.");
            return;
          }
          await updateTenantUser(editing.id, {
            email: values.email || null,
            roleIds: values.roleIds,
            membershipIsActive: values.membershipIsActive ?? true,
            password: pwd.length > 0 ? pwd : null,
          });
        }
      }
      setDrawerOpen(false);
      await refresh();
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.data) {
        mapApiErrorsToForm(err.response.data, setError);
        const fields = (err.response.data as { errors?: Record<string, string[]> })
          ?.errors;
        const first =
          fields &&
          Object.values(fields)
            .flat()
            .find((m) => typeof m === "string" && m.length > 0);
        if (first) {
          setFormError(first);
          return;
        }
      }
      setFormError(
        drawerMode === "create"
          ? "Create user failed. Check User Name / roles / permissions."
          : "Update user failed. Check roles / password / permissions.",
      );
    }
  });

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add User"
      : drawerMode === "view"
        ? "View User"
        : "Edit User";

  return (
    <SettingsPageChrome
      active="users"
      title="Users"
      breadcrumb={null}
      fill
      actions={
        <ClaimGate formPath={FORM_PATH} action="Create">
          <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
            <PlusIcon /> Add User
          </button>
        </ClaimGate>
      }
    >
      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Users" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <DataCard fill>
        <DataTable<TenantUserDto>
          columns={columns}
          data={pageRows}
          getRowKey={(r) => r.membershipId}
          emptyMessage="No users in this company yet."
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
                r.membershipIsActive ? "Deactivate membership" : "Already inactive"
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
        title="Deactivate user?"
        description={
          pendingDelete ? (
            <>
              Soft-deactivate will disable{" "}
              <span className="font-semibold text-[#1F2937]">
                {pendingDelete.userName}
              </span>{" "}
              for this company. The login stays; membership becomes inactive.
            </>
          ) : null
        }
        confirmLabel="Deactivate user"
        cancelLabel="Keep active"
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
          drawerMode === "create" || drawerMode === "edit" ? "Save User" : undefined
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
              User Name
            </span>
            <input
              className={inputClass(Boolean(errors.userName))}
              placeholder="Enter user name…"
              disabled={readOnly || drawerMode === "edit"}
              aria-invalid={Boolean(errors.userName)}
              {...register("userName")}
            />
            {errors.userName ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.userName.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span
              className={
                readOnly
                  ? fieldLabelMutedClass(drawerMode === "create")
                  : fieldLabelClass(drawerMode === "create")
              }
            >
              Password
              {drawerMode === "edit" ? (
                <span className="ml-1 font-normal text-[#8090A8]">(leave blank to keep)</span>
              ) : null}
            </span>
            <input
              type="password"
              className={inputClass(Boolean(errors.password))}
              placeholder={
                drawerMode === "edit" ? "New password (optional)…" : "Enter password…"
              }
              disabled={readOnly}
              autoComplete="new-password"
              aria-invalid={Boolean(errors.password)}
              {...register("password")}
            />
            {errors.password ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.password.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Email
            </span>
            <input
              className={inputClass(Boolean(errors.email))}
              placeholder="Enter email…"
              disabled={readOnly}
              aria-invalid={Boolean(errors.email)}
              {...register("email")}
            />
            {errors.email ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.email.message}
              </span>
            ) : null}
          </label>

          {drawerMode === "create" && isSuperAdmin ? (
            <Controller
              name="companyId"
              control={control}
              render={({ field }) => (
                <SearchableCompanySelect
                  label="Company"
                  required
                  options={companies}
                  value={field.value ?? null}
                  placeholder="Select company for this user"
                  error={
                    formError?.toLowerCase().includes("company")
                      ? formError
                      : undefined
                  }
                  onBlur={field.onBlur}
                  onChange={field.onChange}
                />
              )}
            />
          ) : null}

          {/* Super Admin edit/view: multi-company memberships (T1.56) */}
          {drawerMode !== "create" && editing && isSuperAdmin ? (
            <div className="space-y-3">
              <span className={fieldLabelMutedClass()}>Companies</span>
              <div className="space-y-3">
                {(editing.companies ?? []).length === 0 ? (
                  <p className="rounded-lg border border-[#E5E7EB] bg-[#F8FAFC] px-3 py-2 text-sm text-[#6B7280]">
                    No company memberships yet.
                  </p>
                ) : (
                  (editing.companies ?? []).map((c) => {
                    const cid = Number(c.companyId);
                    const draft = membershipDrafts[cid] ?? {
                      roleIds: (c.roles ?? []).map((r) => Number(r.roleId)),
                      membershipIsActive: c.membershipIsActive,
                    };
                    const companyRoles = rolesForCompany(cid);
                    return (
                      <div
                        key={cid}
                        className="space-y-2 rounded-lg border border-[#E5E7EB] bg-white px-3 py-3"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <p className="text-sm font-semibold text-[#1A2238]">
                              {c.companyName}
                            </p>
                          </div>
                          {!readOnly ? (
                            <button
                              type="button"
                              disabled={membershipBusy || !c.membershipIsActive}
                              className="text-[11px] font-semibold text-[#B91C1C] hover:underline disabled:opacity-40"
                              onClick={() => void deactivateMembershipRow(cid)}
                            >
                              Deactivate
                            </button>
                          ) : null}
                        </div>
                        <div
                          className={[
                            "max-h-36 space-y-1 overflow-y-auto rounded-md border px-2.5 py-2",
                            readOnly ? "border-[#E5E7EB] bg-[#F8FAFC]" : "border-[#E5E7EB]",
                          ].join(" ")}
                        >
                          {companyRoles.length === 0 ? (
                            <p className="text-xs text-[#8090A8]">No roles for this company.</p>
                          ) : (
                            companyRoles.map((r) => {
                              const checked = draft.roleIds.includes(r.id);
                              return (
                                <label
                                  key={r.id}
                                  className="flex cursor-pointer items-center gap-2 text-sm text-[#374151]"
                                >
                                  <input
                                    type="checkbox"
                                    className="h-4 w-4 accent-[#2563EB]"
                                    disabled={readOnly || membershipBusy}
                                    checked={checked}
                                    onChange={() => {
                                      const roleIds = checked
                                        ? draft.roleIds.filter((id) => id !== r.id)
                                        : [...draft.roleIds, r.id];
                                      setMembershipDrafts((prev) => ({
                                        ...prev,
                                        [cid]: { ...draft, roleIds },
                                      }));
                                    }}
                                  />
                                  <span>
                                    {r.name}{" "}
                                    <span className="font-mono text-[11px] text-[#8090A8]">
                                      ({r.roleCode})
                                    </span>
                                  </span>
                                </label>
                              );
                            })
                          )}
                        </div>
                        {!readOnly ? (
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <label className="flex items-center gap-2 text-xs text-[#374151]">
                              <input
                                type="checkbox"
                                className="h-3.5 w-3.5 accent-[#2563EB]"
                                disabled={membershipBusy}
                                checked={draft.membershipIsActive}
                                onChange={(e) =>
                                  setMembershipDrafts((prev) => ({
                                    ...prev,
                                    [cid]: {
                                      ...draft,
                                      membershipIsActive: e.target.checked,
                                    },
                                  }))
                                }
                              />
                              Active in this company
                            </label>
                            <button
                              type="button"
                              disabled={membershipBusy}
                              className="rounded-md bg-[#2563EB] px-2.5 py-1 text-[11px] font-semibold text-white disabled:opacity-50"
                              onClick={() => void applyMembership(cid)}
                            >
                              Save company
                            </button>
                          </div>
                        ) : (
                          <span
                            className={[
                              "text-[11px] font-semibold",
                              c.membershipIsActive ? "text-[#2563EB]" : "text-[#64748B]",
                            ].join(" ")}
                          >
                            {c.membershipIsActive ? "Active" : "Inactive"}
                          </span>
                        )}
                      </div>
                    );
                  })
                )}
              </div>

              {!readOnly && companiesAvailableToAdd.length > 0 ? (
                <div className="space-y-2 rounded-lg border border-dashed border-[#CBD5E1] bg-[#F8FAFC] px-3 py-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[#64748B]">
                    Add company
                  </p>
                  <SearchableCompanySelect
                    label="Company"
                    required
                    options={companiesAvailableToAdd}
                    value={addCompanyId}
                    placeholder="Select another company…"
                    onBlur={() => undefined}
                    onChange={(id) => setAddCompanyId(id)}
                  />
                  <div className="max-h-36 space-y-1 overflow-y-auto rounded-md border border-[#E5E7EB] bg-white px-2.5 py-2">
                    {addCompanyRoles.length === 0 ? (
                      <p className="text-xs text-[#8090A8]">
                        Select a company to load roles.
                      </p>
                    ) : (
                      addCompanyRoles.map((r) => {
                        const checked = addRoleIds.includes(r.id);
                        return (
                          <label
                            key={r.id}
                            className="flex cursor-pointer items-center gap-2 text-sm text-[#374151]"
                          >
                            <input
                              type="checkbox"
                              className="h-4 w-4 accent-[#2563EB]"
                              disabled={membershipBusy}
                              checked={checked}
                              onChange={() => {
                                setAddRoleIds((prev) =>
                                  checked
                                    ? prev.filter((id) => id !== r.id)
                                    : [...prev, r.id],
                                );
                              }}
                            />
                            <span>
                              {r.name}{" "}
                              <span className="font-mono text-[11px] text-[#8090A8]">
                                ({r.roleCode})
                              </span>
                            </span>
                          </label>
                        );
                      })
                    )}
                  </div>
                  <button
                    type="button"
                    disabled={membershipBusy}
                    className="rounded-md border border-[#2563EB] px-2.5 py-1.5 text-[11px] font-semibold text-[#2563EB] disabled:opacity-50"
                    onClick={() => void addMembership()}
                  >
                    Add company membership
                  </button>
                </div>
              ) : null}
            </div>
          ) : null}

          {/* Tenant Admin / create: single company roles */}
          {drawerMode === "create" || !isSuperAdmin ? (
            <>
              {drawerMode !== "create" && editing && !isSuperAdmin ? (
                <div className="block space-y-1.5">
                  <span className={fieldLabelMutedClass()}>Company</span>
                  <div className="rounded-lg border border-[#E5E7EB] bg-[#F8FAFC] px-3 py-2 text-sm text-[#374151]">
                    {editing.companyName || "—"}
                  </div>
                </div>
              ) : null}

              <fieldset className="block space-y-2">
                <legend
                  className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}
                >
                  Roles
                </legend>
                <Controller
                  name="roleIds"
                  control={control}
                  render={({ field }) => (
                    <div
                      className={[
                        "max-h-48 space-y-1.5 overflow-y-auto rounded-lg border px-3 py-2",
                        errors.roleIds ? "border-[#DC2626]" : "border-[#E5E7EB]",
                        readOnly ? "bg-[#F8FAFC]" : "bg-white",
                      ].join(" ")}
                    >
                      {roles.length === 0 ? (
                        <p className="text-xs text-[#8090A8]">No company roles available.</p>
                      ) : (
                        roles.map((r) => {
                          const checked = field.value?.includes(r.id) ?? false;
                          return (
                            <label
                              key={r.id}
                              className="flex cursor-pointer items-center gap-2 text-sm text-[#374151]"
                            >
                              <input
                                type="checkbox"
                                className="h-4 w-4 accent-[#2563EB]"
                                disabled={readOnly}
                                checked={checked}
                                onChange={() => {
                                  const next = checked
                                    ? (field.value ?? []).filter((id) => id !== r.id)
                                    : [...(field.value ?? []), r.id];
                                  field.onChange(next);
                                }}
                              />
                              <span>
                                {r.name}{" "}
                                <span className="font-mono text-[11px] text-[#8090A8]">
                                  ({r.roleCode})
                                </span>
                              </span>
                            </label>
                          );
                        })
                      )}
                    </div>
                  )}
                />
                {errors.roleIds ? (
                  <span className={fieldErrorClass()} role="alert">
                    {errors.roleIds.message}
                  </span>
                ) : (
                  <span className="text-[11px] text-[#8090A8]">
                    Select one or more roles. Permissions = union of all assigned roles.
                  </span>
                )}
              </fieldset>

              {drawerMode === "edit" && !isSuperAdmin ? (
                <label className="flex items-center gap-2 text-sm text-[#374151]">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-[#2563EB]"
                    {...register("membershipIsActive")}
                  />
                  Active in this company
                </label>
              ) : null}
            </>
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
