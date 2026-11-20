"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppHeader } from "@/components/layout/app-header";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { AppFooter } from "@/components/layout/app-footer";
import { FormRouteGate } from "@/components/auth/form-route-gate";
import { sessionStore } from "@/lib/auth/session";
import { invalidateMyPermissionsCache } from "@/lib/auth/use-my-permissions";

/**
 * ==========================================================================================
 * WHAT: Authenticated Main-screen chrome — navy sidebar + white header + light canvas.
 * WHY: Fixed viewport shell — header + main + DEVELOPED BY always visible (no page scroll).
 * EXAMPLE: Listing pages fill main; table body scrolls inside the card.
 * ==========================================================================================
 */
export function TenantShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [ready, setReady] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    if (!sessionStore.isAuthenticated()) {
      router.replace("/login");
      return;
    }
    setReady(true);
  }, [router]);

  function logout() {
    sessionStore.clear();
    invalidateMyPermissionsCache();
    router.replace("/login");
  }

  function toggleSidebar() {
    if (typeof window !== "undefined" && window.innerWidth < 1024) {
      setMobileOpen((v) => !v);
      return;
    }
    setCollapsed((v) => !v);
  }

  if (!ready) {
    return (
      <div className="flex h-screen items-center justify-center bg-[#F7F8FA] text-sm text-[#6B7280]">
        Loading workspace…
      </div>
    );
  }

  return (
    <div className="flex h-screen overflow-hidden bg-[#F5F7FB] text-[#1F2937]">
      <div className="z-20 hidden h-full shrink-0 lg:block">
        <AppSidebar collapsed={collapsed} />
      </div>

      {mobileOpen ? (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button
            type="button"
            aria-label="Close menu"
            className="absolute inset-0 bg-[#0B1B33]/50 backdrop-blur-[1px]"
            onClick={() => setMobileOpen(false)}
          />
          <div className="absolute inset-y-0 left-0 w-[248px] shadow-2xl">
            <AppSidebar onNavigate={() => setMobileOpen(false)} />
          </div>
        </div>
      ) : null}

      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <AppHeader onToggleSidebar={toggleSidebar} onLogout={logout} />
        <main className="flex min-h-0 w-full min-w-0 max-w-[100%] flex-1 flex-col overflow-hidden">
          <FormRouteGate>{children}</FormRouteGate>
        </main>
        <AppFooter />
      </div>
    </div>
  );
}
