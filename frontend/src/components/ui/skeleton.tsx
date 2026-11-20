import type { ComponentPropsWithoutRef } from "react";

type SkeletonProps = ComponentPropsWithoutRef<"div"> & {
  className?: string;
};

export function Skeleton({ className = "", ...rest }: SkeletonProps) {
  return (
    <div
      aria-hidden
      className={`animate-pulse rounded-sm bg-zinc-200/80 ${className}`}
      {...rest}
    />
  );
}

export function TableSkeleton({
  rows = 8,
  columns = 5,
}: {
  rows?: number;
  columns?: number;
}) {
  return (
    <div className="border border-zinc-200 bg-white" role="status" aria-label="Loading table">
      <div className="grid gap-0 border-b border-zinc-200 bg-zinc-50 px-3 py-3"
        style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}
      >
        {Array.from({ length: columns }).map((_, i) => (
          <Skeleton key={`h-${i}`} className="h-3 w-20" />
        ))}
      </div>
      {Array.from({ length: rows }).map((_, r) => (
        <div
          key={`r-${r}`}
          className="grid items-center gap-3 border-b border-zinc-100 px-3 py-3"
          style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}
        >
          {Array.from({ length: columns }).map((_, c) => (
            <Skeleton key={`c-${r}-${c}`} className="h-3 w-full max-w-[9rem]" />
          ))}
        </div>
      ))}
    </div>
  );
}

export function MetricsSkeleton() {
  return (
    <div className="grid gap-3 sm:grid-cols-3" role="status" aria-label="Loading metrics">
      {Array.from({ length: 3 }).map((_, i) => (
        <div key={i} className="border border-zinc-200 bg-white p-4 space-y-3">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-7 w-16" />
        </div>
      ))}
    </div>
  );
}
