"use client";

/**
 * ==========================================================================================
 * WHAT: Form × Action checkbox tree for Roles drawer (newSalon RoleWebFormsAction UX).
 * WHY: SSoT grants are RoleFormActions — not free-form PermissionCatalog codes.
 * EXAMPLE: Forms listed; each shows only catalog FormActions as checkboxes.
 * ==========================================================================================
 */

import type { FormWithActionsDto } from "@/types/rbac";

export type FormActionSelection = Record<number, number[]>;

export function FormActionMatrix({
  catalog,
  selected,
  onChange,
  disabled,
}: {
  catalog: FormWithActionsDto[];
  selected: FormActionSelection;
  onChange: (next: FormActionSelection) => void;
  disabled?: boolean;
}) {
  function toggle(formId: number, actionId: number) {
    if (disabled) return;
    const current = selected[formId] ?? [];
    const nextIds = current.includes(actionId)
      ? current.filter((id) => id !== actionId)
      : [...current, actionId];
    onChange({ ...selected, [formId]: nextIds });
  }

  function toggleAllForForm(form: FormWithActionsDto, checked: boolean) {
    if (disabled) return;
    onChange({
      ...selected,
      [form.id]: checked ? form.actions.map((a) => a.id) : [],
    });
  }

  if (catalog.length === 0) {
    return (
      <p className="rounded-lg border border-[#E8ECF2] bg-[#F8FAFC] px-3 py-4 text-sm text-[#8090A8]">
        No Form×Action catalog yet. Seed Forms/Actions or assign actions on Forms.
      </p>
    );
  }

  return (
    <div className="max-h-[320px] space-y-3 overflow-y-auto rounded-lg border border-[#E8ECF2] bg-white p-3">
      {catalog.map((form) => {
        const chosen = new Set(selected[form.id] ?? []);
        const allChecked =
          form.actions.length > 0 && form.actions.every((a) => chosen.has(a.id));
        return (
          <div key={form.id} className="space-y-2 border-b border-[#F1F5F9] pb-3 last:border-0">
            <label className="flex items-center gap-2 text-sm font-semibold text-[#1A2238]">
              <input
                type="checkbox"
                className="h-4 w-4 accent-[#2563EB]"
                checked={allChecked}
                disabled={disabled || form.actions.length === 0}
                onChange={(e) => toggleAllForForm(form, e.target.checked)}
              />
              <span>{form.name}</span>
              <span className="font-mono text-[11px] font-normal text-[#8090A8]">
                {form.path}
              </span>
            </label>
            <div className="flex flex-wrap gap-x-4 gap-y-2 pl-6">
              {form.actions.map((action) => (
                <label
                  key={action.id}
                  className="inline-flex items-center gap-1.5 text-[13px] text-[#374151]"
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-[#2563EB]"
                    checked={chosen.has(action.id)}
                    disabled={disabled}
                    onChange={() => toggle(form.id, action.id)}
                  />
                  {action.name}
                </label>
              ))}
              {form.actions.length === 0 ? (
                <span className="text-[12px] text-[#94A3B8]">No actions linked</span>
              ) : null}
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function selectionFromRoleForms(
  forms: FormWithActionsDto[] | undefined,
): FormActionSelection {
  const next: FormActionSelection = {};
  for (const f of forms ?? []) {
    next[f.id] = (f.actions ?? []).map((a) => a.id);
  }
  return next;
}

export function selectionToSyncItems(selected: FormActionSelection) {
  return Object.entries(selected)
    .map(([formId, actionIds]) => ({
      formId: Number(formId),
      actionIds: actionIds ?? [],
    }))
    .filter((x) => x.formId > 0 && x.actionIds.length > 0);
}
