"use client";

import { useSuspenseQuery } from "@tanstack/react-query";
import { getCurrentTenant } from "@/lib/api/endpoints";
import { MASTER_DATA_GC_MS, MASTER_DATA_STALE_MS } from "@/lib/query/client";
import { queryKeys } from "@/lib/query/keys";
import { useCategoriesSuspense } from "@/hooks/use-categories";

export function TenantMetricsPanel() {
  const { data: tenant } = useSuspenseQuery({
    queryKey: queryKeys.tenant.me,
    queryFn: getCurrentTenant,
    staleTime: MASTER_DATA_STALE_MS,
    gcTime: MASTER_DATA_GC_MS,
  });

  const { data: categories } = useCategoriesSuspense();

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <MetricCard label="Company" value={tenant.name} />
        <MetricCard
          label="Status"
          value={tenant.isActive ? "Active" : "Inactive"}
        />
      </div>

      <div className="border border-zinc-200 bg-white p-4">
        <p className="text-xs uppercase tracking-wide text-zinc-500">
          Category master (cached {MASTER_DATA_STALE_MS / 60_000}m)
        </p>
        <p className="mt-1 text-2xl font-semibold tabular-nums">
          {categories.length}
        </p>
        <p className="text-xs text-zinc-500">
          Dropdown dataset — no refetch on navigation while fresh.
        </p>
      </div>
    </div>
  );
}

function MetricCard({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="border border-zinc-200 bg-white p-4">
      <p className="text-xs uppercase tracking-wide text-zinc-500">{label}</p>
      <p className={`mt-1 text-lg font-semibold ${mono ? "font-mono text-sm" : ""}`}>
        {value}
      </p>
    </div>
  );
}
