/**
 * ==========================================================================================
 * WHAT: Login UI tenant option — company name for display, id as company_id INT for submit.
 * WHY: SearchableCompanySelect shows name; RHF+Zod onBlur validates the numeric company_id.
 * EXAMPLE: { id: 12, name: "Acme Corp", tenantCode: "ACME" } → form company_id: 12.
 * ==========================================================================================
 */
export interface LoginTenantOption {
  id: number;
  name: string;
  tenantCode: string;
}
