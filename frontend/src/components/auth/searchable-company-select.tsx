"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";

export type CompanyOption = {
  id: number;
  name: string;
};

type Props = {
  label: string;
  required?: boolean;
  options: CompanyOption[];
  value: number | null;
  placeholder?: string;
  disabled?: boolean;
  error?: string;
  onBlur: () => void;
  onChange: (companyId: number | null) => void;
};

/**
 * ==========================================================================================
 * WHAT: Searchable company dropdown that displays company name but emits company_id INT.
 * WHY: RHF+Zod onBlur needs a numeric company_id; UI errors render in #DC2626 (border-red-600).
 * EXAMPLE: Select "Acme Corp" → onChange(42); onBlur runs for Zod "Company selection is required."
 * ==========================================================================================
 */
export function SearchableCompanySelect({
  label,
  required,
  options,
  value,
  placeholder = "Select your company / organization",
  disabled,
  error,
  onBlur,
  onChange,
}: Props) {
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const selected = options.find((o) => o.id === value) ?? null;

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) => o.name.toLowerCase().includes(q));
  }, [options, query]);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) {
        setOpen(false);
        onBlur();
      }
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [onBlur]);

  const border = error
    ? "border-red-600 focus-within:border-red-600"
    : "border-[#D1D5DB] focus-within:border-[#2563EB] focus-within:shadow-[0_0_0_3px_rgba(37,99,235,0.12)]";

  return (
    <div ref={rootRef} className="space-y-1.5">
      <label className="block text-[12.5px] font-semibold text-[#374151]">
        {label}
        {required ? <span className="ml-0.5 text-red-600">*</span> : null}
      </label>

      <div className={`relative rounded-lg border bg-white transition-[border-color,box-shadow] ${border}`}>
        <button
          type="button"
          disabled={disabled}
          aria-expanded={open}
          aria-controls={listId}
          className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-[13px] text-[#1F2937] outline-none disabled:opacity-60"
          onClick={() => setOpen((v) => !v)}
          onBlur={() => {
            if (!open) onBlur();
          }}
        >
          <span className={selected ? "text-[#1F2937]" : "text-[#9CA3AF]"}>
            {selected?.name ?? placeholder}
          </span>
          <ChevronIcon open={open} />
        </button>

        {open ? (
          <div
            id={listId}
            className="absolute left-0 right-0 top-[calc(100%+4px)] z-30 overflow-hidden rounded-lg border border-[#E5E7EB] bg-white shadow-[0_10px_24px_rgba(15,23,42,0.12)]"
          >
            <div className="border-b border-[#F3F4F6] p-2">
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search companies…"
                className="w-full rounded-md border border-[#E5E7EB] px-2.5 py-1.5 text-[12.5px] outline-none focus:border-[#2563EB]"
              />
            </div>
            <ul className="max-h-48 overflow-y-auto py-1">
              {filtered.length === 0 ? (
                <li className="px-3 py-2 text-[12.5px] text-[#6B7280]">No companies found.</li>
              ) : (
                filtered.map((opt) => (
                  <li key={opt.id}>
                    <button
                      type="button"
                      className={`flex w-full px-3 py-2 text-left text-[13px] hover:bg-[#EFF6FF] ${
                        opt.id === value ? "bg-[#EFF6FF] font-semibold text-[#1D4ED8]" : "text-[#1F2937]"
                      }`}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => {
                        onChange(opt.id);
                        setQuery("");
                        setOpen(false);
                        onBlur();
                      }}
                    >
                      {opt.name}
                    </button>
                  </li>
                ))
              )}
            </ul>
          </div>
        ) : null}
      </div>

      {error ? (
        <p className="text-[11px] text-red-600" role="alert">
          {error}
        </p>
      ) : null}
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
