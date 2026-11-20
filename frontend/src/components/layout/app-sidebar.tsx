"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { usePathname } from "next/navigation";
import { MAIN_NAV, isNavItemActive, type NavSection } from "@/components/layout/main-nav";
import { NavIcon } from "@/components/layout/nav-icons";
import { canAccessHref } from "@/lib/auth/permission-codes";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";

/**
 * ==========================================================================================
 * WHAT: Sidebar with SETTINGS as a collapsible dropdown (children listed underneath).
 * WHY: Remove duplicate Settings tabs; keep Catalog–Financials flat, Settings nested.
 * EXAMPLE: Click SETTINGS → Company / Roles / Actions / …
 * ==========================================================================================
 */
export function AppSidebar({
  collapsed,
  onNavigate,
}: {
  collapsed?: boolean;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();
  const { codes, ready, hasForm, forms } = useMyPermissions();
  const settingsOpenDefault = pathname.startsWith("/settings");
  const [settingsOpen, setSettingsOpen] = useState(settingsOpenDefault);

  useEffect(() => {
    if (pathname.startsWith("/settings")) {
      setSettingsOpen(true);
    }
  }, [pathname]);

  const sections = useMemo(() => {
    return MAIN_NAV.map((section) => {
      const visibleItems = section.items.filter((item) => {
        if (!ready) return item.href === "/dashboard";
        if (forms.length > 0) {
          return hasForm(item.href);
        }
        return canAccessHref(codes, item.href);
      });
      return { ...section, items: visibleItems };
    }).filter((s) => s.items.length > 0);
  }, [codes, ready, hasForm, forms.length]);

  return (
    <aside
      data-scrollbar="dark"
      className={[
        "flex h-full flex-col bg-[#0B1B33] text-white",
        collapsed ? "w-[72px]" : "w-[248px]",
      ].join(" ")}
    >
      <div
        className={[
          "flex h-16 shrink-0 items-center border-b border-white/[0.08] lg:h-[68px]",
          collapsed ? "justify-center px-2" : "px-2.5",
        ].join(" ")}
      >
        <Link href="/dashboard" onClick={onNavigate} className="block w-full" aria-label="XENASYS Market home">
          {collapsed ? (
            <Image
              src="/brand/xenasys-mark.png"
              alt="XENASYS"
              width={40}
              height={40}
              priority
              unoptimized
              className="mx-auto h-9 w-9 object-contain"
            />
          ) : (
            <Image
              src="/brand/xenasys-market-logo-sidebar.png"
              alt="XENASYS Market — Multi-Tenant Commerce"
              width={320}
              height={56}
              priority
              unoptimized
              className="h-11 w-full object-contain object-left lg:h-12"
            />
          )}
        </Link>
      </div>

      <nav
        data-scrollbar="dark"
        className="min-h-0 flex-1 space-y-2 overflow-y-auto px-2 py-2"
      >
        {sections.map((section, idx) => {
          const isSettings = section.title === "SETTINGS";

          if (isSettings && !collapsed) {
            return (
              <SettingsDropdown
                key="settings"
                section={section}
                open={settingsOpen}
                onToggle={() => setSettingsOpen((v) => !v)}
                pathname={pathname}
                onNavigate={onNavigate}
              />
            );
          }

          return (
            <div key={section.title ?? `top-${idx}`} className="space-y-px">
              {section.title && !collapsed ? (
                <p className="px-2.5 pb-0.5 pt-1 text-[9px] font-semibold tracking-[0.14em] text-[#8090A8]">
                  {section.title}
                </p>
              ) : null}
              {section.items.map((item) => (
                <NavLinkRow
                  key={item.href}
                  href={item.href}
                  label={item.label}
                  icon={item.icon}
                  active={isNavItemActive(pathname, item.href)}
                  collapsed={collapsed}
                  onNavigate={onNavigate}
                />
              ))}
            </div>
          );
        })}
      </nav>
    </aside>
  );
}

function SettingsDropdown({
  section,
  open,
  onToggle,
  pathname,
  onNavigate,
}: {
  section: NavSection & { items: NavSection["items"] };
  open: boolean;
  onToggle: () => void;
  pathname: string;
  onNavigate?: () => void;
}) {
  const childActive = section.items.some((i) => isNavItemActive(pathname, i.href));

  return (
    <div className="space-y-px">
      <button
        type="button"
        onClick={onToggle}
        className={[
          "flex w-full items-center justify-between rounded-lg px-2.5 py-[6px] text-[12.5px] font-semibold transition-colors",
          childActive || open
            ? "bg-white/[0.08] text-white"
            : "text-[#B7C0CF] hover:bg-white/[0.06] hover:text-white",
        ].join(" ")}
        aria-expanded={open}
      >
        <span className="flex items-center gap-2.5">
          <NavIcon name="settings" className="h-4 w-4 text-[#9AA6B8]" />
          <span>Settings</span>
        </span>
        <Chevron open={open} />
      </button>

      {open ? (
        <div className="ml-2 space-y-px border-l border-white/[0.08] pl-2">
          {section.items.map((item) => (
            <NavLinkRow
              key={item.href}
              href={item.href}
              label={item.label}
              icon={item.icon}
              active={isNavItemActive(pathname, item.href)}
              onNavigate={onNavigate}
              nested
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function NavLinkRow({
  href,
  label,
  icon,
  active,
  collapsed,
  nested,
  onNavigate,
}: {
  href: string;
  label: string;
  icon: Parameters<typeof NavIcon>[0]["name"];
  active: boolean;
  collapsed?: boolean;
  nested?: boolean;
  onNavigate?: () => void;
}) {
  return (
    <Link
      href={href}
      title={label}
      onClick={onNavigate}
      className={[
        "group flex items-center gap-2.5 rounded-lg text-[12.5px] font-medium transition-colors",
        nested ? "px-2.5 py-[5px]" : "px-2.5 py-[6px]",
        active
          ? "bg-[#2563EB] text-white"
          : "text-[#B7C0CF] hover:bg-white/[0.06] hover:text-white",
        collapsed ? "justify-center px-2" : "",
      ].join(" ")}
    >
      <NavIcon
        name={icon}
        className={[
          "h-4 w-4 shrink-0",
          active ? "text-white" : "text-[#9AA6B8] group-hover:text-white",
        ].join(" ")}
      />
      {!collapsed ? <span className="truncate">{label}</span> : null}
    </Link>
  );
}

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      className={["transition-transform", open ? "rotate-180" : ""].join(" ")}
      aria-hidden
    >
      <path
        d="m6 9 6 6 6-6"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
