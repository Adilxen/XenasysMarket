"use client";

/**
 * ==========================================================================================
 * WHAT: Settings → Company listing + right drawer CRUD (Category Listing pattern / module SSoT).
 * WHY: List registered tenants; View / Edit / Delete row actions; viewport-height table body.
 * EXAMPLE: Route /settings/company → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createTenant,
  deactivateTenant,
  getTenant,
  listTenants,
  updateTenant,
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
import { zNoHtmlString } from "@/lib/security/sanitize";
import type { TenantDto } from "@/types/api";

const FORM_PATH = "/settings/company";

const companyFormSchema = z.object({
  name: zNoHtmlString().trim().min(2, "Company name is required.").max(200),
  contactEmail: z
    .string()
    .trim()
    .max(256)
    .refine((v) => v === "" || z.string().email().safeParse(v).success, {
      message: "Contact email is invalid.",
    }),
  contactPhone: zNoHtmlString().trim().max(30).optional().or(z.literal("")),
  address: zNoHtmlString().trim().max(500).optional().or(z.literal("")),
});

type CompanyFormInput = z.infer<typeof companyFormSchema>;

type DrawerMode = "view" | "create" | "edit";

export function CompanyListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);

  const [rows, setRows] = useState<TenantDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<TenantDto | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [pendingDelete, setPendingDelete] = useState<TenantDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "name",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CompanyFormInput>({
    resolver: zodResolver(companyFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      name: "",
      contactEmail: "",
      contactPhone: "",
      address: "",
    },
  });

  async function refresh(filters: Record<string, string> = columnFilters) {
    const data = await listTenants({ filters });
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
          setLoadError(
            "Unable to load companies. Check company.read claim or re-login after API restart.",
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
  const pageRows = useMemo<TenantDto[]>(() => {
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, pageNumber, pageSize]);

  const columns = useMemo<DataTableColumn<TenantDto>[]>(
    () => [
      {
        key: "name",
        label: "Company Name",
        width: 220,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.name}</span>,
      },
      {
        key: "contactEmail",
        label: "Email",
        width: 200,
        filter: false,
        render: (r) => (
          <span className="text-[#6B7280]">{r.contactEmail || "—"}</span>
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

  function fillForm(row: TenantDto | null) {
    reset({
      name: row?.name ?? "",
      contactEmail: row?.contactEmail ?? "",
      contactPhone: row?.contactPhone ?? "",
      address: row?.address ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: TenantDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getTenant(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable for core fields */
      }
    })();
  }

  function openEdit(row: TenantDto) {
    if (!canUpdate) {
      setLoadError("Edit requires company.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getTenant(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full company detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    try {
      if (drawerMode === "create") {
        await createTenant({
          name: values.name,
          contactEmail: values.contactEmail || null,
          contactPhone: values.contactPhone || null,
          address: values.address || null,
        });
      } else if (editing) {
        await updateTenant(editing.id, {
          name: values.name,
          contactEmail: values.contactEmail || null,
          contactPhone: values.contactPhone || null,
          address: values.address || null,
        });
      }
      setDrawerOpen(false);
      await refresh();
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Register company failed. Super Admin only; retry if code allocation collided."
          : "Update company failed. Check company.update claim.",
      );
    }
  });

  function requestDelete(row: TenantDto) {
    if (!canDelete) {
      setLoadError("Delete requires company.delete claim. Re-login after API restart.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Company is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateTenant(pendingDelete.id);
      setPendingDelete(null);
      await refresh();
    } catch {
      setLoadError("Delete failed. Check company.delete claim and tenant scope.");
    } finally {
      setDeleting(false);
    }
  }

  const readOnly = drawerMode === "view";

  const drawerTitle =
    drawerMode === "create"
      ? "Add Company"
      : drawerMode === "view"
        ? "View Company"
        : "Edit Company";

  return (
    <SettingsPageChrome
      active="company"
      title="Company"
      breadcrumb={null}
      fill
      actions={
        <ClaimGate formPath={FORM_PATH} action="Create">
          <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
            <PlusIcon /> Add Company
          </button>
        </ClaimGate>
      }
    >
      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Company action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <DataCard fill>
        <DataTable<TenantDto>
          columns={columns}
          data={pageRows}
          getRowKey={(r) => r.id}
          emptyMessage="No companies registered yet."
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
        title="Delete company?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.name}</span>
              . The company stays in history but will no longer appear in active listings.
            </>
          ) : null
        }
        confirmLabel="Delete company"
        cancelLabel="Keep company"
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
            ? "Register Company"
            : drawerMode === "edit"
              ? "Save Company"
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
              Company Name
            </span>
            <input
              className={inputClass(Boolean(errors.name))}
              placeholder="Enter company name…"
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

          <div className="grid gap-5 sm:grid-cols-2">
            <label className="block space-y-1.5">
              <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
                Contact Email
              </span>
              <input
                className={inputClass(Boolean(errors.contactEmail))}
                placeholder="ops@company.com"
                disabled={readOnly}
                aria-invalid={Boolean(errors.contactEmail)}
                {...register("contactEmail")}
              />
              {errors.contactEmail ? (
                <span className={fieldErrorClass()} role="alert">
                  {errors.contactEmail.message}
                </span>
              ) : null}
            </label>

            <label className="block space-y-1.5">
              <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
                Contact Phone
              </span>
              <input
                className={inputClass(Boolean(errors.contactPhone))}
                placeholder="+92…"
                disabled={readOnly}
                aria-invalid={Boolean(errors.contactPhone)}
                {...register("contactPhone")}
              />
              {errors.contactPhone ? (
                <span className={fieldErrorClass()} role="alert">
                  {errors.contactPhone.message}
                </span>
              ) : null}
            </label>
          </div>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>Address</span>
            <textarea
              className={inputClass(Boolean(errors.address), "min-h-[96px] resize-y")}
              placeholder="Street, city, country…"
              disabled={readOnly}
              aria-invalid={Boolean(errors.address)}
              {...register("address")}
            />
            {errors.address ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.address.message}
              </span>
            ) : null}
          </label>
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
