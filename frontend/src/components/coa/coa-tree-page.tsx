"use client";

/**
 * ==========================================================================================
 * WHAT: Read-only COA hierarchy tree — Master → Control → Subsidiary.
 * WHY: BRD gap #C6 — topology view alongside flat CRUD listings.
 * EXAMPLE: Route /finance/coa → expandable tree for JWT company (SA sees scoped page).
 * ==========================================================================================
 */

import { useEffect, useMemo, useState } from "react";
import {
  listControlAccounts,
  listMasterAccounts,
  listSubsidiaryAccounts,
} from "@/lib/api/endpoints";
import { Alert } from "@/components/ui/alert";
import { DataCard } from "@/components/ui/data-card";
import { PageHeader } from "@/components/ui/page-header";
import { pageCanvasFillClass } from "@/lib/forms/field-styles";
import type {
  ControlAccountDto,
  MasterAccountDto,
  SubsidiaryAccountDto,
} from "@/types/api";

type TreeControl = ControlAccountDto & { subsidiaries: SubsidiaryAccountDto[] };
type TreeMaster = MasterAccountDto & { controls: TreeControl[] };

export function CoaTreePage() {
  const [tree, setTree] = useState<TreeMaster[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [expandedMasters, setExpandedMasters] = useState<Set<number>>(new Set());
  const [expandedControls, setExpandedControls] = useState<Set<number>>(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [mastersPage, controlsPage, subsPage] = await Promise.all([
          listMasterAccounts({ limit: 100, shape: "list" }),
          listControlAccounts({ limit: 100, shape: "list" }),
          listSubsidiaryAccounts({ limit: 100, shape: "list" }),
        ]);
        if (cancelled) return;

        const controlsByMaster = new Map<number, TreeControl[]>();
        for (const c of controlsPage.items) {
          const node: TreeControl = { ...c, subsidiaries: [] };
          const list = controlsByMaster.get(c.masterAccountId) ?? [];
          list.push(node);
          controlsByMaster.set(c.masterAccountId, list);
        }

        const controlIndex = new Map<number, TreeControl>();
        for (const list of controlsByMaster.values()) {
          for (const c of list) controlIndex.set(c.id, c);
        }
        for (const s of subsPage.items) {
          const parent = controlIndex.get(s.controlAccountId);
          if (parent) parent.subsidiaries.push(s);
        }

        const next: TreeMaster[] = mastersPage.items.map((m) => ({
          ...m,
          controls: controlsByMaster.get(m.id) ?? [],
        }));
        setTree(next);
        setExpandedMasters(new Set(next.map((m) => m.id)));
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load COA tree. Check coa.masters.read claim.");
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const summary = useMemo(() => {
    const controls = tree.reduce((n, m) => n + m.controls.length, 0);
    const subs = tree.reduce(
      (n, m) => n + m.controls.reduce((c, ctrl) => c + ctrl.subsidiaries.length, 0),
      0,
    );
    return { masters: tree.length, controls, subs };
  }, [tree]);

  function toggleMaster(id: number) {
    setExpandedMasters((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleControl(id: number) {
    setExpandedControls((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <div className={pageCanvasFillClass()}>
      <div className="shrink-0">
        <PageHeader
          title="COA Tree"
          description="Master → Control → Subsidiary hierarchy (read-only)."
        />
      </div>

      {loadError ? (
        <div className="shrink-0">
          <Alert tone="warning" title="COA tree" onDismiss={() => setLoadError(null)}>
            {loadError}
          </Alert>
        </div>
      ) : null}

      <p className="mb-3 shrink-0 text-sm text-[#4B5563]">
        Masters {summary.masters} · Controls {summary.controls} · Subsidiaries {summary.subs}
        {summary.controls >= 100 || summary.subs >= 100
          ? " (first 100 of Controls/Subsidiaries shown)"
          : ""}
      </p>

      <div className="flex min-h-0 flex-1 flex-col">
        <DataCard fill>
          <ul className="space-y-1 overflow-auto p-4 text-sm">
            {tree.length === 0 && !loadError ? (
              <li className="text-[#64748B]">No master accounts yet.</li>
            ) : null}
            {tree.map((master) => {
              const masterOpen = expandedMasters.has(master.id);
              return (
                <li key={master.id}>
                  <button
                    type="button"
                    className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left font-semibold text-[#1A2238] hover:bg-[#F3F5F9]"
                    onClick={() => toggleMaster(master.id)}
                  >
                    <span className="w-4 text-[#64748B]">{masterOpen ? "▾" : "▸"}</span>
                    <span className="font-mono text-[#2563EB]">{master.code}</span>
                    <span>{master.title}</span>
                    <span className="text-xs font-normal text-[#64748B]">
                      ({master.prefix})
                    </span>
                  </button>
                  {masterOpen ? (
                    <ul className="ml-6 border-l border-[#E5E7EB] pl-3">
                      {master.controls.length === 0 ? (
                        <li className="py-1 text-[#94A3B8]">No controls</li>
                      ) : null}
                      {master.controls.map((ctrl) => {
                        const ctrlOpen = expandedControls.has(ctrl.id);
                        return (
                          <li key={ctrl.id}>
                            <button
                              type="button"
                              className="flex w-full items-center gap-2 rounded px-2 py-1 text-left text-[#1A2238] hover:bg-[#F3F5F9]"
                              onClick={() => toggleControl(ctrl.id)}
                            >
                              <span className="w-4 text-[#64748B]">
                                {ctrlOpen ? "▾" : "▸"}
                              </span>
                              <span className="font-mono text-xs text-[#4B5563]">
                                {ctrl.code}
                              </span>
                              <span>{ctrl.title}</span>
                            </button>
                            {ctrlOpen ? (
                              <ul className="ml-6 border-l border-[#E5E7EB] pl-3">
                                {ctrl.subsidiaries.length === 0 ? (
                                  <li className="py-1 text-[#94A3B8]">No subsidiaries</li>
                                ) : null}
                                {ctrl.subsidiaries.map((sub) => (
                                  <li
                                    key={sub.id}
                                    className="flex items-center gap-2 px-2 py-1 text-[#4B5563]"
                                  >
                                    <span className="font-mono text-xs">
                                      {sub.accountNo || sub.subsidiaryAccountCode}
                                    </span>
                                    <span>{sub.subsidiaryAccountTitle}</span>
                                  </li>
                                ))}
                              </ul>
                            ) : null}
                          </li>
                        );
                      })}
                    </ul>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </DataCard>
      </div>
    </div>
  );
}
