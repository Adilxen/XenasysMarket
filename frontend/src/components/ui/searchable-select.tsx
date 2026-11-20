"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";

export type SearchableSelectOption = {
  value: string | number;
  label: string;
};

type Props = {
  options: SearchableSelectOption[];
  value: string | number | null | undefined;
  onChange: (value: string | number | null) => void;
  onBlur?: () => void;
  placeholder?: string;
  searchPlaceholder?: string;
  disabled?: boolean;
  error?: boolean;
  /** Sentinels treated as empty (e.g. 0 or ""). */
  emptyValues?: Array<string | number | null | undefined>;
  className?: string;
  "aria-invalid"?: boolean;
  "aria-label"?: string;
};

function isEmpty(
  value: string | number | null | undefined,
  emptyValues: Array<string | number | null | undefined>,
): boolean {
  if (value === null || value === undefined) return true;
  return emptyValues.some((e) => e === value);
}

/**
 * ==========================================================================================
 * WHAT: Reusable searchable dropdown for form FK / filter selects.
 * WHY: Plain <select> is hard to use on large Category/Brand/Unit lists.
 * EXAMPLE: value=12 → shows "Hair Care"; type "hai" filters options.
 * ==========================================================================================
 */
export function SearchableSelect({
  options,
  value,
  onChange,
  onBlur,
  placeholder = "Select…",
  searchPlaceholder = "Search…",
  disabled,
  error,
  emptyValues = [0, "", null, undefined],
  className,
  "aria-invalid": ariaInvalid,
  "aria-label": ariaLabel,
}: Props) {
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const empty = isEmpty(value, emptyValues);
  const selected = empty
    ? null
    : (options.find((o) => o.value === value) ?? null);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) => o.label.toLowerCase().includes(q));
  }, [options, query]);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) {
        setOpen(false);
        onBlur?.();
      }
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [onBlur]);

  const border = error
    ? "border-red-600 focus-within:border-red-600"
    : "border-[#D1D5DB] focus-within:border-[#2563EB] focus-within:shadow-[0_0_0_3px_rgba(37,99,235,0.12)]";

  return (
    <div ref={rootRef} className={`relative ${className ?? ""}`}>
      <div
        className={`rounded-lg border bg-white transition-[border-color,box-shadow] ${border} ${
          disabled ? "opacity-60" : ""
        }`}
      >
        <button
          type="button"
          disabled={disabled}
          aria-expanded={open}
          aria-controls={listId}
          aria-invalid={ariaInvalid ?? error}
          aria-label={ariaLabel ?? placeholder}
          className="flex h-10 w-full items-center justify-between gap-2 px-3 text-left text-[13px] text-[#1F2937] outline-none disabled:cursor-not-allowed"
          onClick={() => {
            if (disabled) return;
            setOpen((v) => !v);
          }}
          onBlur={() => {
            if (!open) onBlur?.();
          }}
        >
          <span className={selected ? "truncate text-[#1F2937]" : "truncate text-[#9CA3AF]"}>
            {selected?.label ?? placeholder}
          </span>
          <ChevronIcon open={open} />
        </button>

        {open && !disabled ? (
          <div
            id={listId}
            className="absolute left-0 right-0 top-[calc(100%+4px)] z-40 overflow-hidden rounded-lg border border-[#E5E7EB] bg-white shadow-[0_10px_24px_rgba(15,23,42,0.12)]"
          >
            <div className="border-b border-[#F3F4F6] p-2">
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={searchPlaceholder}
                className="w-full rounded-md border border-[#E5E7EB] px-2.5 py-1.5 text-[12.5px] outline-none focus:border-[#2563EB]"
              />
            </div>
            <ul className="max-h-48 overflow-y-auto py-1">
              {filtered.length === 0 ? (
                <li className="px-3 py-2 text-[12.5px] text-[#6B7280]">No matches found.</li>
              ) : (
                filtered.map((opt) => (
                  <li key={String(opt.value)}>
                    <button
                      type="button"
                      className={`flex w-full px-3 py-2 text-left text-[13px] hover:bg-[#EFF6FF] ${
                        opt.value === value
                          ? "bg-[#EFF6FF] font-semibold text-[#1D4ED8]"
                          : "text-[#1F2937]"
                      }`}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => {
                        onChange(opt.value);
                        setQuery("");
                        setOpen(false);
                        onBlur?.();
                      }}
                    >
                      {opt.label}
                    </button>
                  </li>
                ))
              )}
            </ul>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function ChevronIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 20 20"
      className={`h-4 w-4 shrink-0 text-zinc-500 transition ${open ? "rotate-180" : ""}`}
      aria-hidden
    >
      <path
        fill="currentColor"
        d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.17l3.71-3.94a.75.75 0 1 1 1.08 1.04l-4.25 4.5a.75.75 0 0 1-1.08 0l-4.25-4.5a.75.75 0 0 1 .02-1.06z"
      />
    </svg>
  );
}
