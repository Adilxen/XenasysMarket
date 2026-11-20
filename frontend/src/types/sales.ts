import type { EntityId } from "@/types/api";

/** Mirrors Xenasys.Core.Contracts.Sales.SalesOrderLineDto */
export interface SalesOrderLineDto {
  id: EntityId;
  productId: EntityId;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/** Mirrors Xenasys.Core.Contracts.Sales.SalesOrderDto */
export interface SalesOrderDto {
  id: EntityId;
  companyId: EntityId;
  companyName?: string | null;
  orderCode: string;
  orderDate: string;
  customerName?: string | null;
  notes?: string | null;
  grossAmount: number;
  status: string;
  isActive: boolean;
  lineCount: number;
  lines?: SalesOrderLineDto[] | null;
}

export interface CreateSalesOrderLineRequest {
  productId: number;
  quantity: number;
  unitPrice?: number | null;
}

export interface CreateSalesOrderRequest {
  companyId?: number | null;
  customerName?: string | null;
  notes?: string | null;
  lines: CreateSalesOrderLineRequest[];
}

export interface UpdateSalesOrderRequest {
  customerName?: string | null;
  notes?: string | null;
}
