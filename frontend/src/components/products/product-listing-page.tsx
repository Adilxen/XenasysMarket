"use client";

/**
 * ==========================================================================================
 * WHAT: Products listing + drawer CRUD (Vendor gold standard; keyset pagination).
 * WHY: BRD §4.5 / M3 — Category+Sub+Brand+Unit FKs; Retail >= Invoice; SKU server-allocated.
 * EXAMPLE: Route /products → DataTable + SideDrawer (view | create | edit)
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  catalogImageUrl,
  createProduct,
  deactivateProduct,
  getProduct,
  listBrands,
  listCategoriesPage,
  listProducts,
  listSubCategories,
  listUnits,
  updateProduct,
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
import type { ProductDto } from "@/types/api";
import type { BrandDto, CategoryDto, SubCategoryDto, UnitDto } from "@/types/catalog";

const FORM_PATH = "/products";

const productFormSchema = z
  .object({
    productName: zNoHtmlString()
      .trim()
      .min(1, "Product Name is required.")
      .max(150, "Product Name must be at most 150 characters."),
    categoryId: z.coerce.number().int().positive("Category is required."),
    subCategoryId: z.coerce.number().int().positive("Sub Category is required."),
    brandId: z.coerce.number().int().positive("Brand is required."),
    unitId: z.coerce.number().int().positive("Unit is required."),
    packaging: zNoHtmlString()
      .trim()
      .min(1, "Packaging is required.")
      .max(50, "Packaging must be at most 50 characters."),
    invoicePrice: z.coerce.number().positive("Invoice Price must be > 0."),
    retailPrice: z.coerce.number().positive("Retail Price must be > 0."),
    minimumStockLevel: z.coerce
      .number()
      .int("Minimum Stock Level must be an integer >= 0.")
      .min(0, "Minimum Stock Level must be >= 0."),
    barcode: z
      .string()
      .trim()
      .max(500, "Barcode must be at most 500 characters.")
      .regex(
        /^[A-Za-z0-9,-]*$/,
        "Barcode may only contain letters, numbers, commas, and hyphens.",
      )
      .optional()
      .or(z.literal("")),
    productDescription: z
      .string()
      .trim()
      .max(1000, "Description must be at most 1000 characters.")
      .optional()
      .or(z.literal("")),
  })
  .refine((d) => d.retailPrice >= d.invoicePrice, {
    message: "Retail Price must be >= Invoice Price.",
    path: ["retailPrice"],
  });

type ProductFormInput = z.infer<typeof productFormSchema>;
type DrawerMode = "view" | "create" | "edit";

function formatMoney(value: number): string {
  return value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function ProductListingPage() {
  const { canCreate, canUpdate, canDelete, canView, canRead } =
    useFormActions(FORM_PATH);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";
  const sessionCompanyId = Number(sessionStore.getTenantId() ?? 0);
  const sessionCompanyName = sessionStore.getCompanyName();

  const [rows, setRows] = useState<ProductDto[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<CategoryDto[]>([]);
  const [subCategoryOptions, setSubCategoryOptions] = useState<SubCategoryDto[]>(
    [],
  );
  const [brandOptions, setBrandOptions] = useState<BrandDto[]>([]);
  const [unitOptions, setUnitOptions] = useState<UnitDto[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<DrawerMode>("create");
  const [editing, setEditing] = useState<ProductDto | null>(null);
  const [pageSize, setPageSize] = useState(10);
  const [cursorStack, setCursorStack] = useState<(number | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<ProductDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);
  const { columnFilters, onColumnFilterChange } = useServerColumnFilters({
    seedFromQuery: "productName",
  });

  const {
    register,
    handleSubmit,
    reset,
    control,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<ProductFormInput>({
    resolver: zodResolver(productFormSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      productName: "",
      categoryId: 0,
      subCategoryId: 0,
      brandId: 0,
      unitId: 0,
      packaging: "",
      invoicePrice: 0,
      retailPrice: 0,
      minimumStockLevel: 0,
      barcode: "",
      productDescription: "",
    },
  });

  const watchedCategoryId = useWatch({ control, name: "categoryId" });

  async function loadPage(
    afterId: number | null,
    filters: Record<string, string> = columnFilters,
  ) {
    const page = await listProducts({
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
      setCatalogError(null);
      if (!Number.isFinite(sessionCompanyId) || sessionCompanyId <= 0) {
        setCategoryOptions([]);
        setBrandOptions([]);
        setUnitOptions([]);
        setCatalogError(
          "Company context missing. Log in again selecting the target company (e.g. TGL).",
        );
        return;
      }
      try {
        // SA bypasses tenant filter — must pass companyId or first 100 rows are mixed tenants.
        const scope = { companyId: sessionCompanyId, limit: 100, shape: "list" as const };
        const [categories, brands, units] = await Promise.all([
          listCategoriesPage(scope),
          listBrands(scope),
          listUnits(),
        ]);
        setCategoryOptions(categories.items);
        setBrandOptions(brands.items);
        setUnitOptions(units);
        if (categories.items.length === 0) {
          setCatalogError(
            `No categories for ${sessionCompanyName ?? `company #${sessionCompanyId}`}. Create Category first.`,
          );
        }
      } catch {
        setCategoryOptions([]);
        setBrandOptions([]);
        setUnitOptions([]);
        setCatalogError("Unable to load Category / Brand / Unit lists for this company.");
      }
    })();
  }, [sessionCompanyId, sessionCompanyName]);

  useEffect(() => {
    const categoryId = Number(watchedCategoryId);
    if (!Number.isFinite(categoryId) || categoryId <= 0) {
      setSubCategoryOptions([]);
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const page = await listSubCategories({
          categoryId,
          companyId: sessionCompanyId > 0 ? sessionCompanyId : undefined,
          limit: 100,
          shape: "list",
        });
        if (!cancelled) setSubCategoryOptions(page.items);
      } catch {
        if (!cancelled) setSubCategoryOptions([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [watchedCategoryId, sessionCompanyId]);

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
            "Unable to load products. Check products.read claim or company context.",
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refetch when filters change
  }, [columnFilters, pageSize]);

  const columns = useMemo<DataTableColumn<ProductDto>[]>(
    () => [
      ...(isSuperAdmin
        ? [
            {
              key: "companyName",
              label: "Company",
              width: 160,
              render: (r: ProductDto) => (
                <span className="text-[#1A2238]">{r.companyName ?? "—"}</span>
              ),
            } satisfies DataTableColumn<ProductDto>,
          ]
        : []),
      {
        key: "productName",
        label: "Product Name",
        width: 200,
        render: (r) => (
          <span className="font-semibold text-[#1A2238]">{r.productName}</span>
        ),
      },
      {
        key: "productCode",
        label: "Product Code",
        width: 150,
        render: (r) => (
          <span className="font-mono text-[#4B5563]">{r.productCode}</span>
        ),
      },
      {
        key: "categoryName",
        label: "Category",
        width: 140,
        render: (r) => (
          <span className="text-[#4B5563]">{r.categoryName ?? "—"}</span>
        ),
      },
      {
        key: "brandName",
        label: "Brand",
        width: 120,
        render: (r) => (
          <span className="text-[#4B5563]">{r.brandName ?? "—"}</span>
        ),
      },
      {
        key: "packaging",
        label: "Packaging",
        width: 110,
        render: (r) => (
          <span className="text-[#4B5563]">{r.packaging?.trim() || "—"}</span>
        ),
      },
      {
        key: "invoicePrice",
        label: "Invoice",
        width: 100,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#4B5563]">
            {formatMoney(r.invoicePrice)}
          </span>
        ),
      },
      {
        key: "retailPrice",
        label: "Retail",
        width: 100,
        filter: false,
        render: (r) => (
          <span className="tabular-nums text-[#4B5563]">
            {formatMoney(r.retailPrice)}
          </span>
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

  function fillForm(row: ProductDto | null) {
    reset({
      productName: row?.productName ?? "",
      categoryId: row?.categoryId ?? 0,
      subCategoryId: row?.subCategoryId ?? 0,
      brandId: row?.brandId ?? 0,
      unitId: row?.unitId ?? 0,
      packaging: row?.packaging ?? "",
      invoicePrice: row?.invoicePrice ?? 0,
      retailPrice: row?.retailPrice ?? 0,
      minimumStockLevel: row?.minimumStockLevel ?? 0,
      barcode: row?.barcode ?? "",
      productDescription: row?.productDescription ?? "",
    });
  }

  function openCreate() {
    setDrawerMode("create");
    setEditing(null);
    setFormError(null);
    setImageFile(null);
    setImageError(null);
    setSubCategoryOptions([]);
    fillForm(null);
    setDrawerOpen(true);
  }

  function openView(row: ProductDto) {
    setDrawerMode("view");
    setEditing(row);
    setFormError(null);
    setImageFile(null);
    setImageError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getProduct(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        /* list row still usable */
      }
    })();
  }

  function openEdit(row: ProductDto) {
    if (!canUpdate) {
      setLoadError("Edit requires products.update claim.");
      return;
    }
    setDrawerMode("edit");
    setEditing(row);
    setFormError(null);
    setImageFile(null);
    setImageError(null);
    fillForm(row);
    setDrawerOpen(true);
    void (async () => {
      try {
        const detail = await getProduct(row.id, "detail");
        setEditing(detail);
        fillForm(detail);
      } catch {
        setLoadError("Unable to load full product detail.");
      }
    })();
  }

  function onImagePicked(file: File | null) {
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
    if (file.size > 2 * 1024 * 1024) {
      setImageError("Image must be at most 2MB.");
      setImageFile(null);
      return;
    }
    setImageFile(file);
  }

  const save = handleSubmit(async (values) => {
    if (drawerMode === "view") return;
    setFormError(null);
    setImageError(null);

    const hasExistingImage = Boolean(catalogImageUrl(editing?.imagePath));
    if (drawerMode === "create" && !imageFile) {
      setImageError("Image is required (JPG/JPEG/PNG, max 2MB).");
      return;
    }
    if (drawerMode === "edit" && !imageFile && !hasExistingImage) {
      setImageError("Image is required (JPG/JPEG/PNG, max 2MB).");
      return;
    }

    const form = new FormData();
    if (isSuperAdmin && sessionCompanyId > 0 && drawerMode === "create") {
      form.append("companyId", String(sessionCompanyId));
    }
    form.append("productName", values.productName.trim());
    form.append("productDescription", values.productDescription?.trim() || "");
    form.append("packaging", values.packaging.trim());
    form.append("categoryId", String(values.categoryId));
    form.append("subCategoryId", String(values.subCategoryId));
    form.append("brandId", String(values.brandId));
    form.append("unitId", String(values.unitId));
    form.append("invoicePrice", String(values.invoicePrice));
    form.append("retailPrice", String(values.retailPrice));
    form.append("minimumStockLevel", String(values.minimumStockLevel));
    form.append("barcode", values.barcode?.trim() || "");
    if (drawerMode === "edit" && editing) {
      form.append("isActive", String(editing.isActive));
    }
    if (imageFile) {
      form.append("image", imageFile);
    }

    try {
      if (drawerMode === "create") {
        await createProduct(form);
      } else if (editing) {
        await updateProduct(editing.id, form);
      }
      setDrawerOpen(false);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch (err) {
      const apiMessage = extractProductApiError(err);
      setFormError(
        apiMessage ??
          (drawerMode === "create"
            ? "Create failed. Check catalog FKs, image, and company context."
            : "Update failed. Check catalog FKs, image, and products.update claim."),
      );
    }
  });

  function extractProductApiError(err: unknown): string | null {
    if (!err || typeof err !== "object" || !("response" in err)) return null;
    const data = (
      err as { response?: { data?: { errors?: Record<string, string[]> } } }
    ).response?.data;
    const apiErrors = data?.errors;
    if (!apiErrors) return null;
    const first = Object.values(apiErrors)
      .flat()
      .find((m) => typeof m === "string" && m.trim());
    return first ?? null;
  }

  function requestDelete(row: ProductDto) {
    if (!canDelete) {
      setLoadError("Delete requires products.delete claim.");
      return;
    }
    if (!row.isActive) {
      setLoadError("Product is already inactive.");
      return;
    }
    setPendingDelete(row);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await deactivateProduct(pendingDelete.id);
      setPendingDelete(null);
      setCursorStack([null]);
      setPageIndex(0);
      await loadPage(null);
    } catch {
      setLoadError(
        "Delete failed. Linked inventory subsidiary remains for ledger trail.",
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

  const imagePreviewUrl = imageFile
    ? URL.createObjectURL(imageFile)
    : catalogImageUrl(editing?.imagePath);

  const drawerTitle =
    drawerMode === "create"
      ? "Add Product"
      : drawerMode === "view"
        ? "View Product"
        : "Edit Product";

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize) || 1);

  return (
    <section className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="Products"
          actions={
            <ClaimGate formPath={FORM_PATH} action="Create">
              <button type="button" className={btnPrimaryClass()} onClick={openCreate}>
                <PlusIcon /> Add Product
              </button>
            </ClaimGate>
          }
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Product action" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <DataTable<ProductDto>
            columns={columns}
            data={rows}
            getRowKey={(r) => r.id}
            emptyMessage="No products registered yet."
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
        title="Delete product?"
        description={
          pendingDelete ? (
            <>
              Soft-delete will deactivate{" "}
              <span className="font-semibold text-[#1F2937]">
                {pendingDelete.productName}
              </span>
              . The linked inventory subsidiary account will remain for ledger
              history.
            </>
          ) : null
        }
        confirmLabel="Delete product"
        cancelLabel="Keep product"
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
            ? "Save Product"
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

          {catalogError ? (
            <Alert tone="warning" title="Catalog" onDismiss={() => setCatalogError(null)}>
              {catalogError}
            </Alert>
          ) : null}

          {drawerMode === "create" && sessionCompanyName ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Registering for company</span>
              <input
                className={inputClass(false)}
                value={sessionCompanyName}
                readOnly
                disabled
              />
            </label>
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

          {drawerMode !== "create" && editing?.productCode ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Product Code</span>
              <input
                className={inputClass(false)}
                value={editing.productCode}
                readOnly
                disabled
              />
            </label>
          ) : null}

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Product Name
            </span>
            <input
              className={inputClass(Boolean(errors.productName))}
              placeholder="e.g. Premium Hair Oil 200ml"
              disabled={readOnly}
              aria-invalid={Boolean(errors.productName)}
              {...register("productName")}
            />
            {errors.productName ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.productName.message}
              </span>
            ) : null}
          </label>

          <div className="space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Product Image
            </span>
            {imagePreviewUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={imagePreviewUrl}
                alt="Product"
                className="h-28 w-28 rounded-lg border border-[#E5E7EB] object-cover"
              />
            ) : null}
            {!readOnly ? (
              <label className={fileDropzoneClass()}>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,image/jpeg,image/png"
                  className="sr-only"
                  onChange={(e) =>
                    onImagePicked(e.target.files?.[0] ?? null)
                  }
                />
                <span className="text-sm text-[#4B5563]">
                  {imageFile
                    ? imageFile.name
                    : drawerMode === "edit" && catalogImageUrl(editing?.imagePath)
                      ? "Replace image (optional)"
                      : "Upload JPG/PNG (max 2MB)"}
                </span>
              </label>
            ) : null}
            {imageError ? (
              <span className={fieldErrorClass()} role="alert">
                {imageError}
              </span>
            ) : null}
          </div>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
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
                  onChange={(v) => {
                    field.onChange(typeof v === "number" ? v : 0);
                    setValue("subCategoryId", 0);
                  }}
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
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Sub Category
            </span>
            <Controller
              name="subCategoryId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={subCategoryOptions.map((s) => ({ value: s.id, label: s.name }))}
                  value={field.value}
                  disabled={readOnly || !watchedCategoryId || Number(watchedCategoryId) <= 0}
                  error={Boolean(errors.subCategoryId)}
                  placeholder="Select sub category"
                  searchPlaceholder="Search sub categories…"
                  aria-invalid={Boolean(errors.subCategoryId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.subCategoryId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.subCategoryId.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Brand
            </span>
            <Controller
              name="brandId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={brandOptions.map((b) => ({ value: b.id, label: b.name }))}
                  value={field.value}
                  disabled={readOnly}
                  error={Boolean(errors.brandId)}
                  placeholder="Select brand"
                  searchPlaceholder="Search brands…"
                  aria-invalid={Boolean(errors.brandId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.brandId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.brandId.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Unit
            </span>
            <Controller
              name="unitId"
              control={control}
              render={({ field }) => (
                <SearchableSelect
                  options={unitOptions.map((u) => ({
                    value: u.id,
                    label: `${u.code} — ${u.name}`,
                  }))}
                  value={field.value}
                  disabled={readOnly}
                  error={Boolean(errors.unitId)}
                  placeholder="Select unit"
                  searchPlaceholder="Search units…"
                  aria-invalid={Boolean(errors.unitId)}
                  onBlur={field.onBlur}
                  onChange={(v) => field.onChange(typeof v === "number" ? v : 0)}
                />
              )}
            />
            {errors.unitId ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.unitId.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Packaging
            </span>
            <input
              className={inputClass(Boolean(errors.packaging))}
              placeholder="e.g. Bottle / Carton"
              disabled={readOnly}
              aria-invalid={Boolean(errors.packaging)}
              {...register("packaging")}
            />
            {errors.packaging ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.packaging.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Invoice Price
            </span>
            <input
              className={inputClass(Boolean(errors.invoicePrice))}
              type="number"
              step="0.01"
              min="0"
              disabled={readOnly}
              aria-invalid={Boolean(errors.invoicePrice)}
              {...register("invoicePrice")}
            />
            {errors.invoicePrice ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.invoicePrice.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Retail Price
            </span>
            <input
              className={inputClass(Boolean(errors.retailPrice))}
              type="number"
              step="0.01"
              min="0"
              disabled={readOnly}
              aria-invalid={Boolean(errors.retailPrice)}
              {...register("retailPrice")}
            />
            {errors.retailPrice ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.retailPrice.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass(true) : fieldLabelClass(true)}>
              Minimum Stock Level
            </span>
            <input
              className={inputClass(Boolean(errors.minimumStockLevel))}
              type="number"
              step="1"
              min="0"
              disabled={readOnly}
              aria-invalid={Boolean(errors.minimumStockLevel)}
              {...register("minimumStockLevel")}
            />
            {errors.minimumStockLevel ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.minimumStockLevel.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={readOnly ? fieldLabelMutedClass() : fieldLabelClass()}>
              Barcode
            </span>
            <input
              className={inputClass(Boolean(errors.barcode))}
              placeholder="Leave empty to auto-generate"
              disabled={readOnly}
              aria-invalid={Boolean(errors.barcode)}
              {...register("barcode")}
            />
            {!readOnly ? (
              <span className="text-xs text-[#6B7280]">
                Optional. Letters, numbers, hyphens; commas for multiple. Empty =
                auto from Product Code. Unique per company.
              </span>
            ) : null}
            {errors.barcode ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.barcode.message}
              </span>
            ) : null}
          </label>

          <label className="block space-y-1.5">
            <span className={fieldLabelMutedClass()}>Description</span>
            <textarea
              className={inputClass(Boolean(errors.productDescription))}
              rows={3}
              placeholder="Optional product description"
              disabled={readOnly}
              aria-invalid={Boolean(errors.productDescription)}
              {...register("productDescription")}
            />
            {errors.productDescription ? (
              <span className={fieldErrorClass()} role="alert">
                {errors.productDescription.message}
              </span>
            ) : null}
          </label>

          {drawerMode === "view" && editing?.subsidiaryAccountId ? (
            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Inventory Subsidiary</span>
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
              Saving allocates Product Code and creates an Inventory subsidiary
              ledger for this SKU.
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
