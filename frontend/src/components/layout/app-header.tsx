"use client";

/**
 * ==========================================================================================
 * WHAT: Main screen header — search, notifications, User Name chip + Profile / Company profile.
 * WHY: Header stays clean (name only); Tenant Admin edits own company from Profile (T1.57 / BRD §3.2).
 * EXAMPLE: Chip "tenantadmin" → Update profile → My Profile + My Company (JWT company only).
 * ==========================================================================================
 */

import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  GlobalCommandSearch,
  GlobalSearchTrigger,
  useGlobalCommandSearch,
} from "@/components/layout/global-command-search";
import { NotificationCenter } from "@/components/layout/notification-center";
import { Alert } from "@/components/ui/alert";
import { SideDrawer } from "@/components/ui/side-drawer";
import {
  getCurrentTenant,
  getMyProfile,
  updateCurrentTenant,
  updateMyProfile,
} from "@/lib/api/endpoints";
import {
  formatRoleLabel,
  initialsFromName,
  sessionStore,
} from "@/lib/auth/session";
import { useMyPermissions } from "@/lib/auth/use-my-permissions";
import {
  fieldErrorClass,
  fieldLabelClass,
  fieldLabelMutedClass,
  inputClass,
} from "@/lib/forms/field-styles";
import { zNoHtmlString } from "@/lib/security/sanitize";

const profileSchema = z.object({
  userName: zNoHtmlString()
    .trim()
    .min(1, "User Name is required.")
    .max(50, "Max 50 characters."),
  password: z
    .string()
    .optional()
    .refine(
      (v) => v == null || v.trim() === "" || v.length >= 8,
      "Password must be at least 8 characters.",
    ),
  roleLabel: z.string().optional(),
  companyName: z.string().optional(),
  companyCode: z.string().optional(),
  companyDisplayName: zNoHtmlString()
    .trim()
    .max(200, "Max 200 characters.")
    .optional()
    .or(z.literal("")),
  contactEmail: z
    .string()
    .trim()
    .email("Invalid email.")
    .max(100)
    .optional()
    .or(z.literal("")),
  contactPhone: zNoHtmlString().trim().max(50).optional().or(z.literal("")),
  address: zNoHtmlString().trim().max(500).optional().or(z.literal("")),
});

type ProfileFormInput = z.infer<typeof profileSchema>;

export function AppHeader({
  onToggleSidebar,
  onLogout,
}: {
  onToggleSidebar: () => void;
  onLogout: () => void;
}) {
  const [displayName, setDisplayName] = useState("User");
  const [userName, setUserName] = useState("");
  const [roleLabel, setRoleLabel] = useState("User");
  const [companyName, setCompanyName] = useState("");
  const [menuOpen, setMenuOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formSuccess, setFormSuccess] = useState<string | null>(null);
  const [companySectionVisible, setCompanySectionVisible] = useState(false);
  const [companyEditable, setCompanyEditable] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const { open, openSearch, closeSearch } = useGlobalCommandSearch();
  const { can, ready: permsReady } = useMyPermissions();

  const jwtCompanyId = Number(sessionStore.getTenantId() || 0);
  const isSuperAdmin =
    sessionStore.getRole()?.toUpperCase() === "SUPER_ADMIN";

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ProfileFormInput>({
    resolver: zodResolver(profileSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      userName: "",
      password: "",
      roleLabel: "",
      companyName: "",
      companyCode: "",
      companyDisplayName: "",
      contactEmail: "",
      contactPhone: "",
      address: "",
    },
  });

  function syncFromSession() {
    setDisplayName(sessionStore.getFormattedDisplayName());
    setUserName(sessionStore.getUserName() ?? "");
    setRoleLabel(formatRoleLabel(sessionStore.getRole()));
    setCompanyName(sessionStore.getCompanyName()?.trim() || "—");
  }

  useEffect(() => {
    syncFromSession();
    let cancelled = false;
    void (async () => {
      try {
        const me = await getMyProfile();
        if (cancelled) return;
        sessionStore.patchProfile(me.userName, me.companyName, me.role);
        setDisplayName(sessionStore.getFormattedDisplayName());
        setUserName(me.userName);
        setRoleLabel(me.roleName?.trim() || formatRoleLabel(me.role));
        setCompanyName(me.companyName?.trim() || "—");
      } catch {
        /* session chip still usable */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!menuRef.current?.contains(e.target as Node)) setMenuOpen(false);
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  function openProfile() {
    setMenuOpen(false);
    setFormError(null);
    setFormSuccess(null);
    const showCompany = jwtCompanyId > 0 && !isSuperAdmin;
    setCompanySectionVisible(showCompany);
    setCompanyEditable(showCompany && (!permsReady || can("company.update")));
    reset({
      userName: userName || sessionStore.getUserName() || "",
      password: "",
      roleLabel,
      companyName,
      companyCode: "",
      companyDisplayName: "",
      contactEmail: "",
      contactPhone: "",
      address: "",
    });
    setProfileOpen(true);
    void (async () => {
      try {
        const me = await getMyProfile();
        const nextRole = me.roleName?.trim() || formatRoleLabel(me.role);
        const nextCompany = me.companyName?.trim() || "—";
        setUserName(me.userName);
        setRoleLabel(nextRole);
        setCompanyName(nextCompany);

        const boundCompany = Number(me.companyId || jwtCompanyId || 0) > 0;
        const show = boundCompany && me.role?.toUpperCase() !== "SUPER_ADMIN";
        setCompanySectionVisible(false);
        setCompanyEditable(false);

        let companyCode = "";
        let companyDisplayName = "";
        let contactEmail = "";
        let contactPhone = "";
        let address = "";

        if (show) {
          try {
            const tenant = await getCurrentTenant();
            companyCode = tenant.tenantCode ?? "";
            companyDisplayName = tenant.name ?? "";
            contactEmail = tenant.contactEmail ?? "";
            contactPhone = tenant.contactPhone ?? "";
            address = tenant.address ?? "";
            setCompanySectionVisible(true);
            setCompanyEditable(!permsReady || can("company.update"));
          } catch {
            /* no company.read — hide My Company block */
          }
        }

        reset({
          userName: me.userName,
          password: "",
          roleLabel: nextRole,
          companyName: nextCompany,
          companyCode,
          companyDisplayName,
          contactEmail,
          contactPhone,
          address,
        });
      } catch {
        /* form still usable from session */
      }
    })();
  }

  const saveProfile = handleSubmit(async (values) => {
    setFormError(null);
    setFormSuccess(null);
    try {
      const pwd = values.password?.trim() ?? "";
      const updated = await updateMyProfile({
        userName: values.userName,
        password: pwd.length > 0 ? pwd : null,
      });

      let nextCompanyName = updated.companyName?.trim() || "—";
      if (companySectionVisible && companyEditable) {
        const name = (values.companyDisplayName ?? "").trim();
        if (!name) {
          setFormError("Company name is required.");
          return;
        }
        const tenant = await updateCurrentTenant({
          name,
          contactEmail: values.contactEmail?.trim() || null,
          contactPhone: values.contactPhone?.trim() || null,
          address: values.address?.trim() || null,
        });
        nextCompanyName = tenant.name?.trim() || nextCompanyName;
        sessionStore.patchProfile(updated.userName, nextCompanyName);
        reset({
          userName: updated.userName,
          password: "",
          roleLabel: updated.roleName?.trim() || formatRoleLabel(updated.role),
          companyName: nextCompanyName,
          companyCode: tenant.tenantCode ?? "",
          companyDisplayName: tenant.name ?? "",
          contactEmail: tenant.contactEmail ?? "",
          contactPhone: tenant.contactPhone ?? "",
          address: tenant.address ?? "",
        });
      } else {
        sessionStore.patchProfile(updated.userName, updated.companyName);
        reset({
          userName: updated.userName,
          password: "",
          roleLabel: updated.roleName?.trim() || formatRoleLabel(updated.role),
          companyName: nextCompanyName,
          companyCode: values.companyCode ?? "",
          companyDisplayName: values.companyDisplayName ?? "",
          contactEmail: values.contactEmail ?? "",
          contactPhone: values.contactPhone ?? "",
          address: values.address ?? "",
        });
      }

      setDisplayName(sessionStore.getFormattedDisplayName());
      setUserName(updated.userName);
      setRoleLabel(updated.roleName?.trim() || formatRoleLabel(updated.role));
      setCompanyName(nextCompanyName);
      setFormSuccess(
        companySectionVisible && companyEditable
          ? "Profile and company updated."
          : "Profile updated.",
      );
    } catch {
      setFormError(
        "Update failed. Check User Name / password / company fields and try again.",
      );
    }
  });

  const initials = initialsFromName(displayName);

  return (
    <>
      <header className="grid h-16 shrink-0 grid-cols-[auto_1fr_auto] items-center gap-3 border-b border-[#E8ECF2] bg-white px-4 lg:h-[68px] lg:gap-4 lg:px-6">
        <button
          type="button"
          aria-label="Toggle sidebar"
          onClick={onToggleSidebar}
          className="inline-flex h-10 w-10 items-center justify-center rounded-xl text-[#4B5563] transition hover:bg-[#F3F4F6]"
        >
          <HamburgerIcon />
        </button>

        <div className="mx-auto hidden w-full max-w-[560px] md:block">
          <GlobalSearchTrigger onOpen={openSearch} />
        </div>

        <div className="flex items-center justify-end gap-2 sm:gap-3">
          <button
            type="button"
            aria-label="Search application"
            onClick={openSearch}
            className="inline-flex h-10 w-10 items-center justify-center rounded-xl text-[#4B5563] transition hover:bg-[#F3F4F6] md:hidden"
          >
            <SearchIcon className="h-5 w-5" />
          </button>

          <NotificationCenter />

          <div ref={menuRef} className="relative">
            <button
              type="button"
              aria-expanded={menuOpen}
              aria-haspopup="menu"
              aria-label={`Account: ${displayName}`}
              onClick={() => setMenuOpen((v) => !v)}
              className="flex max-w-[200px] items-center gap-2.5 rounded-xl py-1 pl-1 pr-2.5 transition hover:bg-[#F8FAFC]"
            >
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#2563EB] text-[13px] font-semibold text-white shadow-[0_4px_10px_rgba(37,99,235,0.25)]">
                {initials}
              </div>
              <p className="hidden min-w-0 truncate text-sm font-semibold text-[#111827] sm:block">
                {displayName}
              </p>
            </button>

            {menuOpen ? (
              <div
                role="menu"
                className="absolute right-0 top-[calc(100%+8px)] z-50 w-[260px] overflow-hidden rounded-xl border border-[#E5E7EB] bg-white shadow-[0_12px_32px_rgba(15,23,42,0.12)]"
              >
                <div className="space-y-2.5 border-b border-[#F3F4F6] px-3.5 py-3">
                  <div>
                    <p className="text-[10px] font-semibold uppercase tracking-wide text-[#8090A8]">
                      User Name
                    </p>
                    <p className="truncate text-sm font-semibold text-[#111827]">{displayName}</p>
                  </div>
                  <div>
                    <p className="text-[10px] font-semibold uppercase tracking-wide text-[#8090A8]">
                      Role
                    </p>
                    <p className="truncate text-sm text-[#374151]">{roleLabel}</p>
                  </div>
                  <div>
                    <p className="text-[10px] font-semibold uppercase tracking-wide text-[#8090A8]">
                      Company
                    </p>
                    <p className="truncate text-sm text-[#374151]">{companyName}</p>
                  </div>
                </div>
                <button
                  type="button"
                  role="menuitem"
                  onClick={openProfile}
                  className="flex w-full px-3.5 py-2.5 text-left text-sm text-[#111827] hover:bg-[#F8FAFC]"
                >
                  Update profile
                </button>
                <button
                  type="button"
                  role="menuitem"
                  onClick={() => {
                    setMenuOpen(false);
                    onLogout();
                  }}
                  className="flex w-full px-3.5 py-2.5 text-left text-sm text-[#B91C1C] hover:bg-[#FEF2F2]"
                >
                  Sign out
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </header>

      <GlobalCommandSearch open={open} onClose={closeSearch} />

      <SideDrawer
        open={profileOpen}
        title="Update profile"
        onClose={() => setProfileOpen(false)}
        primaryLabel="Save profile"
        onPrimary={() => void saveProfile()}
        primaryLoading={isSubmitting}
        wide={companySectionVisible}
      >
        <form className="space-y-5" noValidate onSubmit={(e) => e.preventDefault()}>
          {formError ? (
            <Alert tone="error" title="Save failed" onDismiss={() => setFormError(null)}>
              {formError}
            </Alert>
          ) : null}
          {formSuccess ? (
            <Alert tone="success" title="Saved" onDismiss={() => setFormSuccess(null)}>
              {formSuccess}
            </Alert>
          ) : null}

          <div className="space-y-4">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-[#8090A8]">
              My profile
            </p>

            <label className="block space-y-1.5">
              <span className={fieldLabelClass(true)}>User Name</span>
              <input
                className={inputClass(Boolean(errors.userName))}
                placeholder="Enter user name…"
                aria-invalid={Boolean(errors.userName)}
                autoComplete="username"
                {...register("userName")}
              />
              {errors.userName ? (
                <span className={fieldErrorClass()} role="alert">
                  {errors.userName.message}
                </span>
              ) : null}
            </label>

            <label className="block space-y-1.5">
              <span className={fieldLabelClass()}>
                Password
                <span className="ml-1 font-normal text-[#8090A8]">(leave blank to keep)</span>
              </span>
              <input
                type="password"
                className={inputClass(Boolean(errors.password))}
                placeholder="New password (optional)…"
                autoComplete="new-password"
                aria-invalid={Boolean(errors.password)}
                {...register("password")}
              />
              {errors.password ? (
                <span className={fieldErrorClass()} role="alert">
                  {errors.password.message}
                </span>
              ) : null}
            </label>

            <label className="block space-y-1.5">
              <span className={fieldLabelMutedClass()}>Role</span>
              <input
                className={inputClass(false)}
                disabled
                readOnly
                {...register("roleLabel")}
              />
              <span className="text-[11px] text-[#8090A8]">
                Role assignment is managed by Tenant Admin — not editable here.
              </span>
            </label>
          </div>

          {companySectionVisible ? (
            <div className="space-y-4 border-t border-[#E8ECF2] pt-5">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-[#8090A8]">
                My company
              </p>

              <label className="block space-y-1.5">
                <span
                  className={
                    companyEditable ? fieldLabelClass(true) : fieldLabelMutedClass(true)
                  }
                >
                  Company Name
                </span>
                <input
                  className={inputClass(Boolean(errors.companyDisplayName))}
                  disabled={!companyEditable}
                  aria-invalid={Boolean(errors.companyDisplayName)}
                  {...register("companyDisplayName")}
                />
                {errors.companyDisplayName ? (
                  <span className={fieldErrorClass()} role="alert">
                    {errors.companyDisplayName.message}
                  </span>
                ) : null}
              </label>

              <label className="block space-y-1.5">
                <span className={companyEditable ? fieldLabelClass() : fieldLabelMutedClass()}>
                  Contact Email
                </span>
                <input
                  className={inputClass(Boolean(errors.contactEmail))}
                  disabled={!companyEditable}
                  aria-invalid={Boolean(errors.contactEmail)}
                  {...register("contactEmail")}
                />
                {errors.contactEmail ? (
                  <span className={fieldErrorClass()} role="alert">
                    {errors.contactEmail.message}
                  </span>
                ) : null}
              </label>

              <label className="block space-y-1.5">
                <span className={companyEditable ? fieldLabelClass() : fieldLabelMutedClass()}>
                  Contact Phone
                </span>
                <input
                  className={inputClass(Boolean(errors.contactPhone))}
                  disabled={!companyEditable}
                  aria-invalid={Boolean(errors.contactPhone)}
                  {...register("contactPhone")}
                />
                {errors.contactPhone ? (
                  <span className={fieldErrorClass()} role="alert">
                    {errors.contactPhone.message}
                  </span>
                ) : null}
              </label>

              <label className="block space-y-1.5">
                <span className={companyEditable ? fieldLabelClass() : fieldLabelMutedClass()}>
                  Address
                </span>
                <textarea
                  className={[inputClass(Boolean(errors.address)), "min-h-[72px] resize-y"].join(
                    " ",
                  )}
                  disabled={!companyEditable}
                  aria-invalid={Boolean(errors.address)}
                  {...register("address")}
                />
                {errors.address ? (
                  <span className={fieldErrorClass()} role="alert">
                    {errors.address.message}
                  </span>
                ) : null}
              </label>

              {!companyEditable ? (
                <span className="text-[11px] text-[#8090A8]">
                  Company profile is read-only for your role.
                </span>
              ) : null}
            </div>
          ) : null}
        </form>
      </SideDrawer>
    </>
  );
}

function HamburgerIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M4 7h16M4 12h16M4 17h16" strokeLinecap="round" />
    </svg>
  );
}

function SearchIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <circle cx="11" cy="11" r="6.5" />
      <path d="m16 16 4 4" strokeLinecap="round" />
    </svg>
  );
}
