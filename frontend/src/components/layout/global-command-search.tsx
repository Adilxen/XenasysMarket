"use client";

/**
 * ==========================================================================================
 * WHAT: Global Search Center UI — pages (local) + records via GET /api/search (TOP-N).
 * WHY: Never preload full tables; debounce + AbortController keep palette stable under load.
 * EXAMPLE: Type "ac" → wait 280ms → /api/search?q=ac&limit=8 → grouped hits
 * ==========================================================================================
 */

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { MAIN_NAV } from "@/components/layout/main-nav";
import { globalSearch } from "@/lib/api/endpoints";
import { canAccessHref } from "@/lib/auth/permission-codes";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";
import type { GlobalSearchHitDto } from "@/types/search";

export type SearchResultKind =
  | "page"
  | "company"
  | "user"
  | "role"
  | "product"
  | "category";

export type SearchResultItem = {
  id: string;
  kind: SearchResultKind;
  group: string;
  label: string;
  description?: string;
  href: string;
};

const MIN_QUERY = 2;
const DEBOUNCE_MS = 280;
const RESULT_LIMIT = 8;

const GROUP_ORDER = [
  "Pages",
  "Companies",
  "Users",
  "Roles",
  "Products",
  "Categories",
] as const;

function titleCaseSection(title?: string): string {
  if (!title) return "Pages";
  return title.charAt(0) + title.slice(1).toLowerCase();
}

function buildPageResults(): SearchResultItem[] {
  const items: SearchResultItem[] = [];
  for (const section of MAIN_NAV) {
    const sectionLabel = titleCaseSection(section.title);
    for (const item of section.items) {
      items.push({
        id: `page:${item.href}`,
        kind: "page",
        group: "Pages",
        label: item.label,
        description:
          sectionLabel === "Pages" ? "Go to screen" : `${sectionLabel} module`,
        href: item.href,
      });
    }
  }
  return items;
}

const PAGE_RESULTS = buildPageResults();

function hitToItem(hit: GlobalSearchHitDto): SearchResultItem {
  const kind = (hit.kind || "page") as SearchResultKind;
  const group =
    kind === "company"
      ? "Companies"
      : kind === "user"
        ? "Users"
        : kind === "role"
          ? "Roles"
          : kind === "product"
            ? "Products"
            : kind === "category"
              ? "Categories"
              : "Pages";

  return {
    id: hit.id,
    kind,
    group,
    label: hit.label,
    description: hit.description ?? undefined,
    href: hit.href,
  };
}

export function useGlobalCommandSearch() {
  const [open, setOpen] = useState(false);
  const openSearch = useCallback(() => setOpen(true), []);
  const closeSearch = useCallback(() => setOpen(false), []);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen((v) => !v);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return { open, openSearch, closeSearch, setOpen };
}

export function GlobalCommandSearch({
  open,
  onClose,
}: {
  open: boolean;
  onClose: () => void;
}) {
  const router = useRouter();
  const { codes, ready, hasForm, forms } = useMyPermissions();
  const inputRef = useRef<HTMLInputElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const cacheRef = useRef<Map<string, SearchResultItem[]>>(new Map());

  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const [recordHits, setRecordHits] = useState<SearchResultItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const pagePool = useMemo(() => {
    if (!ready) return [];
    if (forms.length > 0) {
      return PAGE_RESULTS.filter((item) => hasForm(item.href));
    }
    return PAGE_RESULTS.filter((item) => canAccessHref(codes, item.href));
  }, [codes, ready, hasForm, forms.length]);

  const pageHits = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return pagePool.slice(0, 12);
    return pagePool
      .filter(
        (p) =>
          p.label.toLowerCase().includes(q) ||
          (p.description?.toLowerCase().includes(q) ?? false) ||
          p.href.toLowerCase().includes(q),
      )
      .slice(0, RESULT_LIMIT);
  }, [pagePool, query]);

  // Debounced record search — abort prior request; cache short keys client-side.
  useEffect(() => {
    if (!open) return;

    const q = query.trim();
    if (q.length < MIN_QUERY) {
      abortRef.current?.abort();
      abortRef.current = null;
      setRecordHits([]);
      setSearching(false);
      setSearchError(null);
      return;
    }

    const cacheKey = q.toLowerCase();
    const cached = cacheRef.current.get(cacheKey);
    if (cached) {
      setRecordHits(cached);
      setSearching(false);
      setSearchError(null);
      return;
    }

    const timer = window.setTimeout(() => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      setSearching(true);
      setSearchError(null);

      void (async () => {
        try {
          const response = await globalSearch(q, RESULT_LIMIT, controller.signal);
          if (controller.signal.aborted) return;
          const items = response.groups.flatMap((g) => g.hits.map(hitToItem));
          // Do not cache empty payloads — failed/soft-empty searches must retry after fixes.
          if (items.length > 0) {
            if (cacheRef.current.size > 40) {
              const first = cacheRef.current.keys().next().value;
              if (first) cacheRef.current.delete(first);
            }
            cacheRef.current.set(cacheKey, items);
          }
          setRecordHits(items);
        } catch (err) {
          if (controller.signal.aborted) return;
          // Axios cancel / AbortError — ignore (user kept typing).
          const ax = err as { code?: string; name?: string };
          if (ax?.code === "ERR_CANCELED" || ax?.name === "CanceledError") return;
          setRecordHits([]);
          setSearchError("Search temporarily unavailable. Try again.");
        } finally {
          if (!controller.signal.aborted) setSearching(false);
        }
      })();
    }, DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timer);
    };
  }, [query, open]);

  useEffect(() => {
    if (!open) {
      abortRef.current?.abort();
      abortRef.current = null;
      setQuery("");
      setRecordHits([]);
      setSearching(false);
      setSearchError(null);
      setActiveIndex(0);
      return;
    }
    const t = window.setTimeout(() => inputRef.current?.focus(), 10);
    return () => window.clearTimeout(t);
  }, [open]);

  const filtered = useMemo(() => {
    // Pages always merge first; record hits only when query is long enough.
    const q = query.trim();
    if (q.length < MIN_QUERY) return pageHits;
    return [...pageHits, ...recordHits];
  }, [pageHits, recordHits, query]);

  const groups = useMemo(() => {
    const map = new Map<string, SearchResultItem[]>();
    for (const item of filtered) {
      const list = map.get(item.group) ?? [];
      list.push(item);
      map.set(item.group, list);
    }
    return GROUP_ORDER.filter((g) => map.has(g)).map(
      (g) => [g, map.get(g)!] as const,
    );
  }, [filtered]);

  useEffect(() => {
    setActiveIndex(0);
  }, [filtered.length, query]);

  const go = useCallback(
    (item: SearchResultItem) => {
      onClose();
      router.push(item.href);
    },
    [onClose, router],
  );

  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key === "ArrowDown") {
        e.preventDefault();
        setActiveIndex((i) => Math.min(i + 1, Math.max(filtered.length - 1, 0)));
        return;
      }
      if (e.key === "ArrowUp") {
        e.preventDefault();
        setActiveIndex((i) => Math.max(i - 1, 0));
        return;
      }
      if (e.key === "Enter") {
        e.preventDefault();
        const item = filtered[activeIndex];
        if (item) go(item);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, filtered, activeIndex, go, onClose]);

  if (!open) return null;

  let flatIndex = -1;
  const qLen = query.trim().length;
  const showHint = qLen > 0 && qLen < MIN_QUERY;
  const showEmpty =
    !searching && !showHint && filtered.length === 0 && qLen >= MIN_QUERY;

  return (
    <div className="fixed inset-0 z-[80] flex items-start justify-center px-4 pt-[10vh] sm:pt-[12vh]">
      <button
        type="button"
        aria-label="Close search"
        className="absolute inset-0 bg-[#0B1B33]/45 backdrop-blur-[2px]"
        onClick={onClose}
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-label="Search center"
        className="relative z-10 flex w-full max-w-2xl flex-col overflow-hidden rounded-xl border border-[#E5E7EB] bg-white shadow-[0_24px_64px_rgba(15,23,42,0.22)]"
      >
        <div className="flex items-center gap-3 border-b border-[#E8ECF2] px-4">
          <SearchGlyph className="h-4 w-4 shrink-0 text-[#9CA3AF]" />
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search pages & records…"
            className="h-12 w-full bg-transparent text-sm text-[#1A2238] outline-none placeholder:text-[#9CA3AF]"
            autoComplete="off"
            spellCheck={false}
          />
          {searching ? (
            <span className="shrink-0 text-[11px] font-medium text-[#8090A8]">
              Searching…
            </span>
          ) : (
            <kbd className="hidden shrink-0 rounded-md border border-[#E5E7EB] bg-[#F7F8FA] px-1.5 py-0.5 text-[10px] font-semibold text-[#6B7280] sm:inline">
              Esc
            </kbd>
          )}
        </div>

        <div className="max-h-[min(480px,56vh)] overflow-y-auto py-2">
          {showHint ? (
            <p className="px-4 py-6 text-center text-sm text-[#8090A8]">
              Type at least {MIN_QUERY} characters to search table records.
              Pages still match below.
            </p>
          ) : null}

          {searchError ? (
            <p className="px-4 py-2 text-center text-xs text-[#B45309]">{searchError}</p>
          ) : null}

          {showEmpty ? (
            <p className="px-4 py-10 text-center text-sm text-[#8090A8]">
              No results for “{query.trim()}”.
            </p>
          ) : null}

          {!showEmpty
            ? groups.map(([group, items]) => (
                <div key={group} className="px-2 pb-2">
                  <p className="px-2 py-1.5 text-[11px] font-semibold tracking-wide text-[#8090A8]">
                    {group}
                  </p>
                  <ul className="space-y-0.5">
                    {items.map((item) => {
                      flatIndex += 1;
                      const index = flatIndex;
                      const active = index === activeIndex;
                      return (
                        <li key={item.id}>
                          <button
                            type="button"
                            onMouseEnter={() => setActiveIndex(index)}
                            onClick={() => go(item)}
                            className={[
                              "flex w-full items-center gap-3 rounded-lg px-2.5 py-2.5 text-left transition",
                              active
                                ? "bg-[#F3F5F9] text-[#1A2238]"
                                : "text-[#374151] hover:bg-[#F7F9FC]",
                            ].join(" ")}
                          >
                            <KindBadge kind={item.kind} active={active} />
                            <span className="min-w-0 flex-1">
                              <span className="block truncate text-sm font-medium">
                                {item.label}
                              </span>
                              {item.description ? (
                                <span className="mt-0.5 block truncate text-[11px] text-[#8090A8]">
                                  {item.description}
                                </span>
                              ) : null}
                            </span>
                            <ArrowGlyph
                              className={[
                                "h-3.5 w-3.5 shrink-0",
                                active ? "text-[#2563EB]" : "text-[#C5CDD8]",
                              ].join(" ")}
                            />
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              ))
            : null}

          {!showEmpty && qLen === 0 && pageHits.length > 0 ? (
            <p className="px-4 pb-3 pt-1 text-center text-[11px] text-[#9CA3AF]">
              Tip: type 2+ characters to search companies, users, roles, products.
            </p>
          ) : null}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-[#E8ECF2] bg-[#F7F9FC] px-4 py-2.5 text-[11px] text-[#6B7280]">
          <span className="inline-flex items-center gap-1.5">
            <EnterGlyph className="h-3.5 w-3.5" />
            Open result
          </span>
          <span className="hidden sm:inline">
            Server TOP-{RESULT_LIMIT} · Abort on type · ↑↓ · Esc
          </span>
        </div>
      </div>
    </div>
  );
}

export function GlobalSearchTrigger({
  onOpen,
  className = "",
}: {
  onOpen: () => void;
  className?: string;
}) {
  const isMac =
    typeof navigator !== "undefined" &&
    /Mac|iPhone|iPad|iPod/.test(navigator.platform);

  return (
    <button
      type="button"
      onClick={onOpen}
      className={[
        "relative flex h-11 w-full items-center gap-2.5 rounded-xl border border-[#E5E7EB] bg-[#F7F8FA] px-3.5 text-left text-sm text-[#9CA3AF]",
        "transition hover:border-[#D1D5DB] hover:bg-[#F3F4F6]",
        "focus-visible:border-[#2563EB] focus-visible:bg-white focus-visible:shadow-[0_0_0_3px_rgba(37,99,235,0.10)] focus-visible:outline-none",
        className,
      ].join(" ")}
    >
      <SearchGlyph className="h-4 w-4 shrink-0 text-[#9CA3AF]" />
      <span className="min-w-0 flex-1 truncate">Search center…</span>
      <kbd className="pointer-events-none hidden shrink-0 rounded-md border border-[#E5E7EB] bg-white px-1.5 py-0.5 text-[10px] font-semibold text-[#6B7280] shadow-sm sm:inline">
        {isMac ? "⌘" : "Ctrl"} K
      </kbd>
    </button>
  );
}

function KindBadge({ kind, active }: { kind: SearchResultKind; active: boolean }) {
  const label =
    kind === "page"
      ? "Page"
      : kind === "company"
        ? "Co"
        : kind === "user"
          ? "User"
          : kind === "role"
            ? "Role"
            : kind === "product"
              ? "SKU"
              : "Cat";

  return (
    <span
      className={[
        "inline-flex h-7 w-10 shrink-0 items-center justify-center rounded-md text-[10px] font-bold tracking-wide",
        active ? "bg-[#EFF6FF] text-[#2563EB]" : "bg-[#F3F5F9] text-[#64748B]",
      ].join(" ")}
    >
      {label}
    </span>
  );
}

function SearchGlyph({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <circle cx="11" cy="11" r="6.5" />
      <path d="m16 16 4 4" strokeLinecap="round" />
    </svg>
  );
}

function ArrowGlyph({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
      <path d="M5 12h14M13 6l6 6-6 6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function EnterGlyph({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M9 10h9a3 3 0 0 1 3 3v4M9 10 5 14l4 4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
