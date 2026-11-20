"use client";

/**
 * ==========================================================================================
 * WHAT: Sub-Categories listing + drawer CRUD (parent Category cascade).
 * WHY: BRD §4.3 — unique name within Category; optional image; auto Code (SUB01).
 * EXAMPLE: Route /sub-categories → listing + SideDrawer CRUD
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  catalogImageUrl,
  createSubCategory,
  deactivateSubCategory,
  getSubCategory,
  listCategoriesPage,
  listSubCategories,
  updateSubCategory,
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
import { sessionStore } from "@/lib/auth/session";
import { useServerColumnFilters } from "@/hooks/use-server-column-filters";
import {
  btnPrimaryClass,
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  fileDropzoneClass,
  inputClass,
  pageCanvasFillClass,
} from "@/lib/forms/field-styles";
import { zNoHtmlString } from "@/lib/security/sanitize";
import type { CategoryDto, SubCategoryDto } from "@/types/catalog";

const FORM_PATH = "/sub-categories";
const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

const subCategoryFormSchema = z.object({
  categoryId: z.coerce.number().int().positive("Category is required."),
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

type SubCategoryFormInput = z.infer<typeof subCategoryFormSchema>;
type DrawerMode = "view" | "create" | "edit";

export function SubCategoryListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } =
    useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const [rows, setRows] = useState<SubCategoryDto[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<CategoryDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<SubCategoryDto | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<SubCategoryDto | null>(
    null,
  );
  const [deleting, setDeleting] = useState(false);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "name",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<SubCategoryFormInput>({
    resolver: zodResolver(subCategoryFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: { categoryId: 0, name: "", description: "" },
  });

  async function loadPage(
    afterId: number | null,
    filters: Record<string, string> = columnFilters,
  ) {
    const page = await listSubCategories({
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
    void (async () => {
      try {
        const page = await listCategoriesPage({ limit: 200, shape: "list" });
        setCategoryOptions(page.items);
      } catch {
        /* dropdown empty until reload */
      }
    })();
  }, []);

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
            "Unable to load sub-categories. Check categories.read claim.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [columnFilters, pageSize]);

  const columns = useMemo<DataTableColumn<SubCategoryDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 160,
              render: (r: SubCategoryDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<SubCategoryDto>,
          ]
        : []),
      {
        key: "categoryName",
        label: "Category",
        width: 160,
        filter: false,
        render: (r) => (
          <span className="text-[#4B5563]">{r.categoryName ?? "—"}</span>
        ),
      },
      {
        key: "name",
        label: "Name",
        width: 200,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.name}</span>
        ),
      },
      {
        key: "code",
        label: "Code",
        width: 100,
        render: (r) => (
          <span className="font-mono text-[#4B5563]">{r.code}</span>
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

  function fillForm(row: SubCategoryDto | null) {
    reset({
      categoryId: row?.categoryId ?? 0,
      name: row?.name ?? "",
      description: row?.description ?? "",
    });
    setImageFile(null);
    setImageError(null);
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: SubCategoryDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSubCategory(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: SubCategoryDto) {
    if (!canUpdate) {
      setLoadError("Edit requires categories.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getSubCategory(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full sub-category detail.");
      }
    })();
  }

  function onImageChange(file: File | null) {
    setImageError(null);
    if (!file) {
      setImageFile(null);
      return;
    }
    const okType =
      file.type === "image/jpeg" ||
      file.type === "image/jpg" ||
      file.type === "image/png" ||
      /\.(jpe?g|png)$/i.test(file.name);
    if (!okType) {
      setImageError("Image must be JPG, JPEG, or PNG.");
      setImageFile(null);
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setImageError("Image must be at most 2MB.");
      setImageFile(null);
      return;
    }
    setImageFile(file);
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);

    const form = new FormData();
    form.append("categoryId", String(values.categoryId));
    form.append("name", values.name.trim());
    if (values.description?.trim()) {
      form.append("description", values.description.trim());
    }
    if (imageFile) {
      form.append("image", imageFile);
    }
    if (drawerMode === "edit" && editing) {
      form.append("isActive", String(editing.isActive));
    }

    try {
      if (drawerMode === "create") {
        await createSubCategory(form);
      } else if (editing) {
        await updateSubCategory(editing.id, form);
      }
      setDrawerOpen(false);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setFormError(
        drawerMode === "create"
          ? "Create failed. Check parent category and unique name."
          : "Update failed. Check unique name and categories.update claim.",
      );
    }
  });

  function requestDelete(row: SubCategoryDto) {
    if (!canDelete) {
      setLoadError("Delete requires categories.delete claim.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Sub-category is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateSubCategory(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError(
        "Delete failed. Active products may be linked, or claim missing.",
      );
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
      ? "Add Sub Category"
      : drawerMode === "view"
        ? "View Sub Category"
        : "Edit Sub Category";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);
  const previewUrl =
    imageFile != null
      ? URL.createObjectURL(imageFile)
      : catalogImageUrl(editing?.imagePath);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Sub Categories"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button
                type="button"
                className={btnPrimaryClass()}
                onClick={openCreate}
              >
                <PlusIcon /> Add Sub Category
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert
            tone="warning"
            title="Sub-category action"
            onDismiss={() => setLoadError(null)}
          >
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<SubCategoryDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No sub-categories registered yet."
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
        title="Delete sub-category?"
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
        confirmLabel="Delete sub-category"
        cancelLabel="Keep sub-category"
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
            ? "Save Sub Category"
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
              Category
            </span>
            <Controller
              name="categoryId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={categoryOptions.map((c) => ({ value: c.id, label: c.name }))}
                  value={field.value}
                  disabled={readOnly}
                  error={Boolean(errors.categoryId)}
                  placeholder="Select category"
                  searchPlaceholder="Search categories…"
                  aria-invalid={Boolean(errors.categoryId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.categoryId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.categoryId.message}
              </span>
            ) : null}
          </label>

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
              placeholder="e.g. Phones"
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

          {drawerMode !== "create" && editing?.code ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Code</span>
              <input
                className={inputClass(false)}
                value={editing.code}
                readOnly
                disabled
              />
            </label>
          ) : null}

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Description</span>
            <textarea
              className={inputClass(Boolean(errors.description))}
              rows={3}
              disabled={readOnly}
              {...register("description")}
            />
            {errors.description ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.description.message}
              </span>
            ) : null}
          </label>

          <div className="space-y-1.5">
            <span className={fieldLabelMutedClass()}>Image (optional)</span>
            {previewUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={previewUrl}
                alt={editing?.name ?? "Sub-category"}
                className="mb-2 h-28 w-28 rounded-lg border border-[#E5E7EB] object-cover"
              />
            ) : null}
            {!readOnly ? (
              <label className={fileDropzoneClass()}>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,image/jpeg,image/png"
                  className="sr-only"
                  onChange={(e) =>
                    onImageChange(e.target.files?.[0] ?? null)
                  }
                />
                <span className="text-sm font-medium text-[#2563EB]">
                  {imageFile
                    ? imageFile.name
                    : "Upload JPG / PNG (max 2MB)"}
                </span>
              </label>
            ) : null}
            {imageError ? (
              <span className={fieldErrorClass()} role="alert">
                {imageError}
              </span>
            ) : null}
          </div>
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
