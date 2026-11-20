"use client";

/**
 * ==========================================================================================
 * WHAT: Header Notification Center — pending low-stock alerts + ringtone on new count.
 * WHY: BRD stock alerts surface in chrome; poll outbox via GET /api/stock/alerts.
 * EXAMPLE: Bell badge → panel list → Open Stock / Dismiss.
 * ==========================================================================================
 */

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  acknowledgeStockAlert,
  listStockAlerts,
} from "@/lib/api/endpoints";
import { playNotificationRingtone } from "@/lib/audio/notification-ringtone";
import { PERMISSION } from "@/lib/auth/permission-codes";
import { sessionStore } from "@/lib/auth/session";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";
import type { StockAlertDto } from "@/types/api";

const POLL_MS = 20_000;

export function NotificationCenter() {
  const { can, ready } = useMyPermissions();
  const canReadStock = ready && can(PERMISSION.StockRead);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";
  const sessionCompanyId = Number(sessionStore.getTenantId() ?? 0);

  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<StockAlertDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [ackBusy, setAckBusy] = useState<number | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const prevCountRef = useRef<number | null>(null);
  const unlockedAudioRef = useRef(false);

  const refresh = useCallback(async () => {
    if (!canReadStock) {
      setItems([]);
      setTotalCount(0);
      return;
    }

    setLoading(true);
    try {
      const page = await listStockAlerts({
        limit: 12,
        companyId:
          isSuperAdmin && sessionCompanyId > 0 ? sessionCompanyId : undefined,
      });
      const count =
        typeof page.totalCount === "number" ? page.totalCount : page.items.length;
      setItems(page.items);
      setTotalCount(count);

      if (
        prevCountRef.current != null &&
        count > prevCountRef.current &&
        unlockedAudioRef.current
      ) {
        playNotificationRingtone();
      }
      prevCountRef.current = count;
    } catch {
      /* keep last good snapshot */
    } finally {
      setLoading(false);
    }
  }, [canReadStock, isSuperAdmin, sessionCompanyId]);

  useEffect(() => {
    if (!canReadStock) return;
    void refresh();
    const id = window.setInterval(() => void refresh(), POLL_MS);
    return () => window.clearInterval(id);
  }, [canReadStock, refresh]);

  useEffect(() => {
    if (!open) return;
    function onDoc(e: MouseEvent) {
      if (!panelRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);

  async function dismiss(outboxId: number) {
    setAckBusy(outboxId);
    try {
      await acknowledgeStockAlert(outboxId);
      await refresh();
    } catch {
      /* ignore */
    } finally {
      setAckBusy(null);
    }
  }

  if (!canReadStock) {
    return null;
  }

  const badge = totalCount > 99 ? "99+" : String(totalCount);

  return (
    <div ref={panelRef} className="relative">
      <button
        type="button"
        aria-label="Notifications"
        aria-expanded={open}
        aria-haspopup="dialog"
        onClick={() => {
          unlockedAudioRef.current = true;
          setOpen((v) => !v);
          void refresh();
        }}
        className="relative inline-flex h-10 w-10 items-center justify-center rounded-xl text-[#4B5563] transition hover:bg-[#F3F4F6]"
      >
        <BellIcon />
        {totalCount > 0 ? (
          <span className="absolute right-1.5 top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-[#EF4444] px-1 text-[10px] font-bold leading-none text-white ring-2 ring-white">
            {badge}
          </span>
        ) : null}
      </button>

      {open ? (
        <div
          role="dialog"
          aria-label="Notification center"
          className="absolute right-0 top-[calc(100%+8px)] z-50 w-[340px] overflow-hidden rounded-xl border border-[#E5E7EB] bg-white shadow-[0_12px_32px_rgba(15,23,42,0.12)]"
        >
          <div className="flex items-center justify-between border-b border-[#F3F4F6] px-3.5 py-2.5">
            <div>
              <p className="text-sm font-semibold text-[#111827]">
                Notifications
              </p>
              <p className="text-[11px] text-[#6B7280]">
                Low stock alerts
                {loading ? " · refreshing…" : ""}
              </p>
            </div>
            <Link
              href="/stock?lowStockOnly=1"
              onClick={() => setOpen(false)}
              className="text-xs font-semibold text-[#2563EB] hover:underline"
            >
              Open Stock
            </Link>
          </div>

          <ul className="max-h-[360px] overflow-y-auto">
            {items.length === 0 ? (
              <li className="px-3.5 py-8 text-center text-sm text-[#6B7280]">
                No pending alerts.
              </li>
            ) : (
              items.map((a) => (
                <li
                  key={a.outboxId}
                  className="border-b border-[#F3F4F6] px-3.5 py-2.5 last:border-b-0"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-[#111827]">
                        {a.productName || "Product"}
                      </p>
                      <p className="truncate font-mono text-[11px] text-[#6B7280]">
                        {a.productCode}
                      </p>
                      <p className="mt-0.5 text-xs text-[#DC2626]">
                        Qty {a.quantityOnHand} ≤ min {a.minimumStockLevel}
                      </p>
                    </div>
                    <button
                      type="button"
                      disabled={ackBusy === a.outboxId || !a.outboxId}
                      onClick={() => {
                        if (a.outboxId) void dismiss(Number(a.outboxId));
                      }}
                      className="shrink-0 rounded-md px-2 py-1 text-[11px] font-semibold text-[#4B5563] hover:bg-[#F3F4F6] disabled:opacity-50"
                    >
                      {ackBusy === a.outboxId ? "…" : "Dismiss"}
                    </button>
                  </div>
                </li>
              ))
            )}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function BellIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M15 17h5l-1.4-1.4A2 2 0 0 1 18 14.2V11a6 6 0 1 0-12 0v3.2c0 .5-.2 1-.6 1.4L4 17h5" />
      <path d="M9.5 17a2.5 2.5 0 0 0 5 0" />
    </svg>
  );
}
