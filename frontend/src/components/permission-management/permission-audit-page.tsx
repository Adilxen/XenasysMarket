"use client";

/**
 * ==========================================================================================
 * WHAT: Permission Audit — newSalon form→action→roles accordion (company-scoped).
 * WHY: T1.41 — match newSalon audit UX; Xenasys Settings chrome + tokens.
 * EXAMPLE: Expand "Users" → Create shows role chips [Tenant Admin, Buyer].
 * ==========================================================================================
 */

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  getFormActionAuditMatrix,
  listRoles,
} from "@/lib/api/endpoints";
import { Alert } from "@/components/ui/alert";
import { DataCard } from "@/components/ui/data-card";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { PermissionManagementShell } from "@/components/permission-management/permission-management-shell";
import {
  btnSecondaryClass,
  fieldLabelClass,
  inputClass,
} from "@/lib/forms/field-styles";
import { useFormActions } from "@/lib/auth/use-my-permissions";
import type {
  FormActionAuditFormDto,
  FormActionAuditMatrixDto,
  RoleDto,
} from "@/types/rbac";

const FORM_PATH = "/settings/permission-audit";

export function PermissionAuditPage() {
  const { canView, canRead } = useFormActions(FORM_PATH);
  const canSee = canView || canRead;

  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState<number | "">("");
  const [audit, setAudit] = useState<FormActionAuditMatrixDto | null>(null);
  const [expandedForms, setExpandedForms] = useState<number[]>([]);
  const [loading, setLoading] = useState(true);
  const [auditLoading, setAuditLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formSearchTerm, setFormSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const t = window.setTimeout(() => {
      setDebouncedSearch(formSearchTerm.trim());
    }, 300);
    return () => window.clearTimeout(t);
  }, [formSearchTerm]);

  const fetchRoles = useCallback(async () => {
    try {
      const rows = await listRoles({ shape: "list" });
      setRoles(rows.filter((r) => r.isActive && r.roleCode !== "SUPER_ADMIN"));
    } catch {
      /* role filter optional */
    }
  }, []);

  const fetchAudit = useCallback(async () => {
    try {
      setAuditLoading(true);
      setError(null);
      const data = await getFormActionAuditMatrix({
        formSearch: debouncedSearch || undefined,
        roleId: selectedRoleId === "" ? undefined : Number(selectedRoleId),
      });
      setAudit(data);
    } catch {
      setError("Unable to load permission audit.");
      setAudit({ forms: [] });
    } finally {
      setAuditLoading(false);
      setLoading(false);
    }
  }, [debouncedSearch, selectedRoleId]);

  useEffect(() => {
    void fetchRoles();
  }, [fetchRoles]);

  useEffect(() => {
    if (!canSee) {
      setLoading(false);
      return;
    }
    void fetchAudit();
  }, [canSee, fetchAudit]);

  const forms = audit?.forms ?? [];

  const filteredForms = useMemo(() => {
    const q = formSearchTerm.trim().toLowerCase();
    if (!q) return forms;
    return forms.filter(
      (f) =>
        f.formName.toLowerCase().includes(q) ||
        f.formPath.toLowerCase().includes(q),
    );
  }, [forms, formSearchTerm]);

  const selectedRole = useMemo(
    () => roles.find((r) => r.id === Number(selectedRoleId)) ?? null,
    [roles, selectedRoleId],
  );

  const formsCountLabel =
    !auditLoading && forms.length > 0
      ? formSearchTerm.trim()
        ? `${filteredForms.length} of ${forms.length} forms`
        : `${forms.length} forms`
      : null;

  function toggleForm(formId: number) {
    setExpandedForms((prev) =>
      prev.includes(formId) ? prev.filter((id) => id !== formId) : [...prev, formId],
    );
  }

  function handleReset() {
    setSelectedRoleId("");
    setFormSearchTerm("");
    setDebouncedSearch("");
    setExpandedForms([]);
  }

  if (!canSee && !loading) {
    return (
      <PermissionManagementShell
        active="permission-audit"
        title="Permission Audit"
        subtitle="View which roles are assigned to each action on every screen"
      >
        <Alert tone="warning" title="Access restricted">
          You do not have Read/View on Permission Audit.
        </Alert>
      </PermissionManagementShell>
    );
  }

  return (
    <PermissionManagementShell
      active="permission-audit"
      title="Permission Audit"
      subtitle="View which roles are assigned to each action on every screen"
      fill
    >
      {error ? (
        <div className="shrink-0">
          <Alert tone="warning" title="Permission Audit" onDismiss={() => setError(null)}>
            {error}
          </Alert>
        </div>
      ) : null}

      <div className="mb-3 shrink-0">
        <DataCard>
          <div className="flex flex-col gap-3 p-3 sm:flex-row sm:items-end sm:gap-3 sm:p-4">
            <label className="min-w-0 flex-1 space-y-1.5">
              <span className={fieldLabelClass()}>Filter by role (optional)</span>
              <SearchableSelect
                options={[
                  { value: "", label: "All roles" },
                  ...roles.map((r) => ({
                    value: r.id,
                    label: `${r.name} (${r.roleCode})`,
                  })),
                ]}
                value={selectedRoleId === "" ? "" : selectedRoleId}
                placeholder="All roles"
                searchPlaceholder="Search roles…"
                emptyValues={[]}
                onChange={(v) => {
                  if (v === "" || v === null) {
                    setSelectedRoleId("");
                  } else {
                    setSelectedRoleId(Number(v));
                  }
                  setExpandedForms([]);
                }}
              />
            </label>
            <div className="grid shrink-0 grid-cols-2 gap-2 sm:flex">
              <button type="button" className={btnSecondaryClass("h-9")} onClick={handleReset}>
                Reset
              </button>
              <button
                type="button"
                className={btnSecondaryClass("h-9")}
                onClick={() => void fetchAudit()}
                disabled={auditLoading}
              >
                Refresh
              </button>
            </div>
          </div>
        </DataCard>
      </div>

      <DataCard fill>
        <div className="flex h-full min-h-0 flex-col">
          <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-[#E8ECF2] px-3 py-2.5 sm:px-4">
            <span className="text-sm font-semibold text-[#1A2238]">Screens &amp; Actions</span>
            {selectedRole ? (
              <span className="inline-flex max-w-[10rem] truncate rounded-full bg-[#EFF6FF] px-2.5 py-0.5 text-[11px] font-semibold text-[#2563EB]">
                {selectedRole.name}
              </span>
            ) : null}
            <div className="relative min-w-0 flex-1">
              <input
                className={inputClass(false, "h-8 pl-3 text-xs sm:h-9 sm:text-sm")}
                placeholder="Search forms…"
                value={formSearchTerm}
                onChange={(e) => setFormSearchTerm(e.target.value)}
                aria-label="Search forms"
              />
            </div>
            {formsCountLabel ? (
              <span className="shrink-0 text-[11px] tabular-nums text-[#8090A8]">
                {formsCountLabel}
              </span>
            ) : null}
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto p-2 sm:p-3">
            {loading || (auditLoading && forms.length === 0) ? (
              <div className="flex min-h-[160px] items-center justify-center text-sm text-[#8090A8]">
                Loading screens…
              </div>
            ) : forms.length === 0 ? (
              <div className="flex min-h-[160px] flex-col items-center justify-center text-center text-[#8090A8]">
                <p className="text-sm font-medium text-[#1A2238]">No forms found</p>
                <p className="mt-1 text-xs">Try adjusting your search or role filter.</p>
              </div>
            ) : filteredForms.length === 0 ? (
              <div className="flex min-h-[160px] flex-col items-center justify-center text-center text-[#8090A8]">
                <p className="text-sm font-medium text-[#1A2238]">No matching forms</p>
                <p className="mt-1 text-xs">Try a different search term or clear the search.</p>
              </div>
            ) : (
              <div className="space-y-2">
                {filteredForms.map((form) => (
                  <AuditFormRow
                    key={form.formId}
                    form={form}
                    expanded={expandedForms.includes(form.formId)}
                    onToggle={() => toggleForm(form.formId)}
                  />
                ))}
              </div>
            )}
          </div>
        </div>
      </DataCard>
    </PermissionManagementShell>
  );
}

function AuditFormRow({
  form,
  expanded,
  onToggle,
}: {
  form: FormActionAuditFormDto;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-[#E8ECF2] bg-white">
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between gap-2 px-3 py-2.5 text-left hover:bg-[#F8FAFC]"
      >
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-sm font-semibold text-[#1A2238]">
              {form.formName}
            </span>
            <span className="shrink-0 rounded-full bg-[#F3F5F9] px-2 py-0.5 text-[10px] tabular-nums text-[#64748B]">
              {form.assignedActionCount}/{form.totalActionCount} actions assigned
            </span>
          </div>
          <span className="mt-0.5 block truncate font-mono text-[11px] text-[#8090A8]">
            {form.formPath}
          </span>
        </div>
        <span className="shrink-0 text-[#8090A8]" aria-hidden>
          {expanded ? "▾" : "▸"}
        </span>
      </button>

      {expanded ? (
        <div className="space-y-2 border-t border-[#E8ECF2] bg-[#F8FAFC] p-3">
          {form.actions.map((action) => (
            <div
              key={action.actionId}
              className="rounded-md border border-[#E8ECF2] bg-white px-3 py-2.5"
            >
              <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:gap-4">
                <span className="shrink-0 text-sm font-medium text-[#1A2238] sm:min-w-[7rem]">
                  {action.actionName}
                </span>
                <div className="flex min-w-0 flex-1 flex-wrap gap-1.5">
                  {action.roles.length > 0 ? (
                    action.roles.map((role) => (
                      <span
                        key={role.roleId}
                        className="inline-flex items-center rounded-full border border-[#BFDBFE] bg-[#EFF6FF] px-2.5 py-0.5 text-[11px] font-medium text-[#2563EB]"
                        title={role.roleCode}
                      >
                        {role.roleName}
                      </span>
                    ))
                  ) : (
                    <span className="text-xs italic text-[#8090A8]">No roles assigned</span>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}
