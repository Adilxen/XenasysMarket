"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import axios from "axios";
import { loginSchema, type LoginInput } from "@/schemas/auth/login.schema";
import { listLoginCompanies, login } from "@/lib/api/endpoints";
import { mapApiErrorsToForm } from "@/lib/forms/map-api-errors";
import { invalidateMyPermissionsCache } from "@/lib/auth/use-my-permissions";
import { isLoginChallenge } from "@/types/api";
import type { LoginTenantOption } from "@/types/auth";
import { SearchableCompanySelect } from "@/components/auth/searchable-company-select";

/**
 * ==========================================================================================
 * WHAT: Login form styled to Login Page.png — RHF+Zod onBlur, company_id INT, password toggle.
 * WHY: Visual parity (title size, field height, blue CTA) while keeping validation #DC2626.
 * EXAMPLE: User toggles eye on password, picks company, submits → POST /api/auth/login.
 * ==========================================================================================
 */
export function LoginForm() {
  const router = useRouter();
  const [companies, setCompanies] = useState<LoginTenantOption[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);

  const {
    register,
    control,
    handleSubmit,
    setError,
    clearErrors,
    formState: { errors, isSubmitting, isValid, isSubmitted },
  } = useForm<LoginInput>({
    resolver: zodResolver(loginSchema),
    mode: "onBlur",
    reValidateMode: "onChange",
    defaultValues: {
      userName: "",
      password: "",
    },
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const rows = await listLoginCompanies();
        if (!cancelled) setCompanies(rows);
      } catch {
        if (!cancelled) {
          setLoadError("Unable to load companies. Check API connectivity.");
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const result = await login({
        userName: values.userName,
        password: values.password,
        company_id: values.company_id,
      });

      if (isLoginChallenge(result)) {
        setFormError("Select your company from the list and try again.");
        return;
      }

      invalidateMyPermissionsCache();
      const landing =
        typeof result.defaultRoute === "string" &&
        result.defaultRoute.trim().startsWith("/")
          ? result.defaultRoute.trim()
          : "/dashboard";
      router.replace(landing);
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 422) {
        mapApiErrorsToForm(err.response.data, setError);
        return;
      }
      if (axios.isAxiosError(err) && err.response?.status === 401) {
        setFormError("Invalid user name or password.");
        return;
      }
      setFormError("Login failed. Check credentials and company selection.");
    }
  });

  const submitLocked = isSubmitting || (isSubmitted && !isValid);

  return (
    <form onSubmit={onSubmit} className="w-full max-w-[340px] space-y-4" noValidate>
      <header className="space-y-1.5">
        <h1 className="font-[family-name:var(--font-login-display)] text-[28px] font-extrabold leading-none tracking-tight text-[#1A2238] sm:text-[30px]">
          Login
        </h1>
        <p className="text-[12.5px] leading-snug text-[#6B7280]">
          Get access to your Orders, Wishlist and Merchant Central
        </p>
      </header>

      {loadError ? (
        <p className="text-xs text-red-600" role="alert">
          {loadError}
        </p>
      ) : null}

      <div className="space-y-3.5">
        <Controller
          name="company_id"
          control={control}
          render={({ field }) => (
            <SearchableCompanySelect
              label="Select Company"
              required
              options={companies.map((c) => ({ id: c.id, name: c.name }))}
              value={Number.isFinite(field.value) ? field.value : null}
              disabled={companies.length === 0}
              error={errors.company_id?.message}
              onBlur={() => {
                field.onBlur();
              }}
              onChange={(id) => {
                field.onChange(id ?? undefined);
                clearErrors("company_id");
              }}
            />
          )}
        />

        <Field label="User Name" error={errors.userName?.message} trailing={<MailIcon />}>
          <input
            type="text"
            autoComplete="username"
            placeholder="Adil Raza"
            className={inputClass(!!errors.userName)}
            {...register("userName", {
              onChange: () => clearErrors("userName"),
            })}
          />
        </Field>

        <Field
          label="Enter Password"
          error={errors.password?.message}
          action={
            <button
              type="button"
              data-testid="toggle-password"
              aria-label={showPassword ? "Hide password" : "Show password"}
              aria-pressed={showPassword}
              onClick={() => setShowPassword((v) => !v)}
              className="relative z-10 rounded p-0.5 text-[#9CA3AF] transition hover:text-[#4B5563] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#2563EB]"
            >
              {showPassword ? <EyeOffIcon /> : <EyeIcon />}
            </button>
          }
        >
          <input
            {...register("password", {
              onChange: () => clearErrors("password"),
            })}
            type={showPassword ? "text" : "password"}
            autoComplete="current-password"
            data-testid="password-input"
            placeholder="••••••••"
            className={inputClass(!!errors.password)}
          />
        </Field>
      </div>

      {formError ? (
        <p className="text-xs text-red-600" role="alert">
          {formError}
        </p>
      ) : null}

      <button
        type="submit"
        disabled={submitLocked}
        className="mt-0.5 w-full rounded-lg bg-[#2563EB] px-3 py-2.5 text-[13.5px] font-semibold tracking-wide text-white shadow-[0_3px_10px_rgba(37,99,235,0.25)] transition hover:bg-[#1D4ED8] disabled:cursor-not-allowed disabled:opacity-60"
      >
        {isSubmitting ? "Signing in…" : "Login"}
      </button>
    </form>
  );
}

function Field({
  label,
  error,
  trailing,
  action,
  children,
}: {
  label: string;
  error?: string;
  trailing?: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <label className="block text-[12.5px] font-semibold text-[#374151]">{label}</label>
      <div className="relative">
        {children}
        {action ? (
          <span className="pointer-events-auto absolute inset-y-0 right-0 z-20 flex items-center pr-3">
            {action}
          </span>
        ) : trailing ? (
          <span className="pointer-events-none absolute inset-y-0 right-0 z-20 flex items-center pr-3 text-[#9CA3AF]">
            {trailing}
          </span>
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

function inputClass(invalid: boolean) {
  return [
    "relative z-0 w-full rounded-lg border bg-white px-3 py-2 pr-10 text-[13px] text-[#1F2937] outline-none placeholder:text-[#9CA3AF]",
    "transition-[border-color,box-shadow]",
    invalid
      ? "border-red-600 focus:border-red-600"
      : "border-[#D1D5DB] focus:border-[#2563EB] focus:shadow-[0_0_0_3px_rgba(37,99,235,0.12)]",
  ].join(" ");
}

function MailIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="m4 7 8 6 8-6" />
    </svg>
  );
}

function EyeIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden>
      <path d="M3 3l18 18" strokeLinecap="round" />
      <path d="M10.6 10.6a2.5 2.5 0 0 0 3.5 3.5" />
      <path d="M9.9 5.2A10.4 10.4 0 0 1 12 5c6.5 0 10 7 10 7a17.4 17.4 0 0 1-4.1 4.8" />
      <path d="M6.1 6.1A17.5 17.5 0 0 0 2 12s3.5 7 10 7c1.4 0 2.7-.3 3.9-.8" />
    </svg>
  );
}
