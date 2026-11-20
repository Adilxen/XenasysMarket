import type { EntityId } from "@/types/api";

/** Mirrors Xenasys.Core.Contracts.Catalog.UnitDto */
export interface UnitDto {
  id: EntityId;
  code: string;
  name: string;
  isActive: boolean;
}

/** Mirrors Xenasys.Core.Contracts.Catalog.CategoryDto */
export interface CategoryDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  name: string;
  code: string;
  description?: string | null;
  imagePath: string;
  isActive: boolean;
}

/** Lightweight option for product dropdowns (id + name). */
export interface CategoryOptionDto {
  id: EntityId;
  name: string;
}

export interface SubCategoryDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  categoryId: EntityId;
  categoryName?: string | null;
  name: string;
  code: string;
  description?: string | null;
  imagePath?: string | null;
  isActive: boolean;
}

export interface BrandDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface SetProductActiveRequest {
  isActive: boolean;
}
