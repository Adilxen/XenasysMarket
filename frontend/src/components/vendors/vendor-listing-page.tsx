"use client";

/**
 * ==========================================================================================
 * WHAT: Vendors listing + drawer CRUD (Company gold standard; keyset pagination).
 * WHY: BRD §4.6 — NTN mask, phone regex; AP subsidiary auto-created on save (server TX).
 * EXAMPLE: Route /vendors → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  createVendor,
  deactivateVendor,
  getVendor,
  listVendors,
  updateVendor,
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
import type { VendorDto } from "@/types/api";

const FORM_PATH = "/vendors";

const vendorFormSchema = z.object({
  vendorName: zNoHtmlString()
    .trim()
    .min(1, "Vendor Name is required.")
    .max(100, "Vendor Name must be at most 100 characters."),
  ntnNo: z
    .string()
    .trim()
    .superRefine((value, ctx) => {
      const normalized = normalizeNtnMask(value);
      if (!normalized) {
        return; // optional
      }
      if (!/^[0-9]{7}-[0-9]{1}$/.test(normalized)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "NTN No must match format 1234567-8.",
        });
      }
    })
    .optional()
    .or(z.literal("")),
  address: z
    .string()
    .trim()
    .max(250, "Address must be at most 250 characters.")
    .optional()
    .or(z.literal("")),
  vendorPhone: z
    .string()
    .trim()
    .min(1, "Phone No is required.")
    .transform((v) => normalizePkMobile(v))
    .refine(
      (v) => /^\+92[0-9]{10}$/.test(v),
      "Phone No must match format +923368288338 (+92 + 10 digits).",
    ),
  email: z
    .string()
    .trim()
    .max(256)
    .refine((v) => v === "" || z.string().email().safeParse(v).success, {
      message: "Enter a valid email address (name@domain.com).",
    }),
  representativeName: z
    .string()
    .trim()
    .max(100, "Representative Name must be at most 100 characters.")
    .optional()
    .or(z.literal("")),
  representativeNo: z
    .string()
    .trim()
    .transform((v) => normalizePkMobile(v))
    .refine(
      (v) => v === "" || /^\+92[0-9]{10}$/.test(v),
      "Representative No must match format +923368288338 (+92 + 10 digits).",
    ),
});

/** Documents NTN mask 1234567-8; 8 bare digits → auto hyphen. */
function normalizeNtnMask(raw: string): string {
  const compact = raw.trim().replace(/\s/g, "");
  if (!compact) return "";
  const digits = compact.replace(/\D/g, "");
  if (digits.length === 8) {
    return `${digits.slice(0, 7)}-${digits.slice(7)}`;
  }
  return compact;
}

/** +92XXXXXXXXXX — strips spaces/dashes; 0XXXXXXXXXX → +92XXXXXXXXXX. */
function normalizePkMobile(raw: string): string {
  let compact = raw.trim().replace(/[\s-]/g, "");
  if (!compact) return "";
  if (!compact.startsWith("+")) compact = `+${compact}`;
  if (/^\+0[0-9]{10}$/.test(compact)) {
    compact = `+92${compact.slice(2)}`;
  }
  return compact;
}

type VendorFormInput = z.infer<typeof vendorFormSchema>;

type DrawerMode = "view" | "create" | "edit";

export function VendorListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } = useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const [rows, setRows] = useState<VendorDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<VendorDto | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<VendorDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "vendorName",
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<VendorFormInput>({
    resolver: zodResolver(vendorFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      vendorName: "",
      ntnNo: "",
      address: "",
      vendorPhone: "",
      email: "",
      representativeName: "",
      representativeNo: "",
    },
  });

  async function loadPage(afterId: number | null, filters: Record<string, string> = columnFilters) {
    const page = await listVendors({
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
            "Unable to load vendors. Check vendors.read claim or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refetch when filters change
  }, [columnFilters, pageSize]);

  const columns = useMemo<DataTableColumn<VendorDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 160,
              render: (r: VendorDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<VendorDto>,
          ]
        : []),
      {
        key: "vendorName",
        label: "Vendor Name",
        width: 200,
        render: (r) => <span className="font-semibold text-[#1A2238]">{r.vendorName}</span>,
      },
      {
        key: "vendorPhone",
        label: "Phone No",
        width: 140,
        render: (r) => <span className="text-[#4B5563]">{r.vendorPhone?.trim() || "—"}</span>,
      },
      {
        key: "ntnNo",
        label: "NTN No",
        width: 120,
        render: (r) => (
          <span className="font-mono text-[#4B5563]">{r.ntnNo?.trim() || "—"}</span>
        ),
      },
      {
        key: "email",
        label: "Email",
        width: 200,
        filter: false,
        render: (r) => (
          <span className="text-[#6B7280]">{r.email?.trim() || "—"}</span>
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
              r.isActive ? "bg-[#EFF6FF] text-[#2563EB]" : "bg-[#F3F5F9] text-[#64748B]",
            ].join(" ")}
          >
            {r.isActive ? "Active" : "Inactive"}
          </span>
        ),
      },
    ],
    [isSuperAdmin],
  );

  function fillForm(row: VendorDto | null) {
    reset({
      vendorName: row?.vendorName ?? "",
      ntnNo: row?.ntnNo ?? "",
      address: row?.address ?? "",
      vendorPhone: row?.vendorPhone ?? "",
      email: row?.email ?? "",
      representativeName: row?.representativeName ?? "",
      representativeNo: row?.representativeNo ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: VendorDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getVendor(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: VendorDto) {
    if (!canUpdate) {
      setLoadError("Edit requires vendors.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getVendor(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full vendor detail.");
      }
    })();
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    const payload = {
      vendorName: values.vendorName.trim(),
      ntnNo: (() => {
        const normalized = normalizeNtnMask(values.ntnNo ?? "");
        return normalized || null;
      })(),
      address: values.address?.trim() || null,
      vendorPhone: values.vendorPhone.trim(),
      email: values.email?.trim() || null,
      representativeName: values.representativeName?.trim() || null,
      representativeNo: values.representativeNo?.trim() || null,
    };
    try {
      if (drawerMode === "create") {
        await createVendor(payload);
      } else if (editing) {
        await updateVendor(editing.id, {
          ...payload,
          isActive: editing.isActive,
        });
      }
      setDrawerOpen(false);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch (err) {
      const apiMessage = extractVendorApiError(err);
      setFormError(
        apiMessage ??
          (drawerMode === "create"
            ? "Create failed. Check company context and try again."
            : "Update failed. Check NTN uniqueness and vendors.update claim."),
      );
    }
  });

  function extractVendorApiError(err: unknown): string | null {
    if (!err || typeof err !== "object" || !("response" in err)) return null;
    const data = (err as { response?: { data?: { errors?: Record<string, string[]> } } })
      .response?.data;
    const errors = data?.errors;
    if (!errors) return null;
    const first = Object.values(errors).flat().find((m) => typeof m === "string" && m.trim());
    return first ?? null;
  }

  function requestDelete(row: VendorDto) {
    if (!canDelete) {
      setLoadError("Delete requires vendors.delete claim.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Vendor is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateVendor(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError("Delete failed. Linked AP subsidiary remains for ledger trail.");
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
      ? "Add Vendor"
      : drawerMode === "view"
        ? "View Vendor"
        : "Edit Vendor";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Vendors"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
                <PlusIcon /> Add Vendor
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Vendor action" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<VendorDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No vendors registered yet."
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
        title="Delete vendor?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">{pendingDelete.vendorName}</span>.
              The linked AP subsidiary account will remain for ledger history.
            </>
          ) : null
        }
        confirmLabel="Delete vendor"
        cancelLabel="Keep vendor"
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
        primaryLabel={drawerMode === "create" || drawerMode === "edit" ? "Save Vendor" : undefined}
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
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Vendor Name
            </span>
            <input
              className={inputClass(Boolean(errors.vendorName))}
              placeholder="e.g. Karachi Packaging Mills"
              disabled={readOnly}
              aria-invalid={Boolean(errors.vendorName)}
              {...register("vendorName")}
            />
            {errors.vendorName ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.vendorName.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Phone No
            </span>
            <input
              className={inputClass(Boolean(errors.vendorPhone))}
              placeholder="+923368288338"
              disabled={readOnly}
              aria-invalid={Boolean(errors.vendorPhone)}
              inputMode="tel"
              maxLength={13}
              {...register("vendorPhone")}
            />
            {errors.vendorPhone ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.vendorPhone.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              NTN No
            </span>
            <input
              className={inputClass(Boolean(errors.ntnNo))}
              placeholder="1234567-8"
              disabled={readOnly}
              aria-invalid={Boolean(errors.ntnNo)}
              inputMode="numeric"
              autoComplete="off"
              maxLength={9}
              {...register("ntnNo", {
                setValueAs: (v) => normalizeNtnMask(String(v ?? "")),
                onChange: (e) => {
                  const digits = String(e.target.value ?? "")
                    .replace(/\D/g, "")
                    .slice(0, 8);
                  e.target.value =
                    digits.length <= 7
                      ? digits
                      : `${digits.slice(0, 7)}-${digits.slice(7)}`;
                },
              })}
            />
            {errors.ntnNo ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.ntnNo.message}
              </span>
            ) : null}
            {!errors.ntnNo && !readOnly ? (
              <span className="text-xs text-[#6B7280]">
                Optional — format 1234567-8 when provided.
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Address
            </span>
            <textarea
              className={inputClass(Boolean(errors.address))}
              rows={3}
              placeholder="Business address"
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

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Email</span>
            <input
              className={inputClass(Boolean(errors.email))}
              type="email"
              placeholder="vendor@example.com"
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

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Representative Name
            </span>
            <input
              className={inputClass(Boolean(errors.representativeName))}
              disabled={readOnly}
              aria-invalid={Boolean(errors.representativeName)}
              {...register("representativeName")}
            />
            {errors.representativeName ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.representativeName.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Representative No
            </span>
            <input
              className={inputClass(Boolean(errors.representativeNo))}
              placeholder="+923368288338"
              disabled={readOnly}
              aria-invalid={Boolean(errors.representativeNo)}
              inputMode="tel"
              maxLength={13}
              {...register("representativeNo")}
            />
            {errors.representativeNo ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.representativeNo.message}
              </span>
            ) : null}
          </label>

          {drawerMode === "view" && editing?.subsidiaryAccountId ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>AP Subsidiary Account</span>
              <input
                className={inputClass(false)}
                value={`#${editing.subsidiaryAccountId} (system-generated)`}
                readOnly
                disabled
              />
            </label>
          ) : null}

          {drawerMode === "create" ? (
            <p className="text-[12px] text-[#8090A8]">
              Saving creates an Accounts Payable subsidiary ledger titled &quot;Vendor: [Name]&quot;.
            </p>
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
