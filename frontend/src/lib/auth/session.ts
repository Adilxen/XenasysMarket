const ACCESS_TOKEN_KEY = "xenasys.accessToken";
const TENANT_ID_KEY = "xenasys.tenantId";
const ROLE_KEY = "xenasys.role";
const USER_NAME_KEY = "xenasys.userName";
const DISPLAY_NAME_KEY = "xenasys.displayName";
const COMPANY_NAME_KEY = "xenasys.companyName";

function read(key: string): string | null {
  if (typeof window === "undefined") return null;
  return sessionStorage.getItem(key);
}

export const sessionStore = {
  getAccessToken(): string | null {
    return read(ACCESS_TOKEN_KEY);
  },

  getTenantId(): string | null {
    return read(TENANT_ID_KEY);
  },

  getRole(): string | null {
    return read(ROLE_KEY);
  },

  getUserName(): string | null {
    return read(USER_NAME_KEY);
  },

  getDisplayName(): string | null {
    return read(DISPLAY_NAME_KEY) ?? read(USER_NAME_KEY);
  },

  getCompanyName(): string | null {
    return read(COMPANY_NAME_KEY);
  },

  /** Header chip: live login UserName (no hardcoded aliases). */
  getFormattedDisplayName(): string {
    return formatDisplayName(this.getDisplayName() ?? this.getUserName());
  },

  /**
   * BRD §4.1 — persist JWT + tenant_id after login for header injection.
   * Display name / role / company feed the Main screen header chip.
   */
  setSession(
    accessToken: string,
    tenantId: string,
    role: string,
    userName?: string,
    displayName?: string,
    companyName?: string,
  ): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    sessionStorage.setItem(TENANT_ID_KEY, tenantId);
    sessionStorage.setItem(ROLE_KEY, role);
    if (userName) sessionStorage.setItem(USER_NAME_KEY, userName);
    if (displayName) sessionStorage.setItem(DISPLAY_NAME_KEY, displayName);
    else if (userName) sessionStorage.setItem(DISPLAY_NAME_KEY, userName);
    if (companyName) sessionStorage.setItem(COMPANY_NAME_KEY, companyName);
  },

  /** After self-service profile save — refresh display fields without new JWT. */
  patchProfile(
    userName: string,
    companyName?: string | null,
    role?: string | null,
  ): void {
    sessionStorage.setItem(USER_NAME_KEY, userName);
    sessionStorage.setItem(DISPLAY_NAME_KEY, userName);
    if (companyName != null && companyName.trim() !== "") {
      sessionStorage.setItem(COMPANY_NAME_KEY, companyName);
    }
    if (role != null && role.trim() !== "") {
      sessionStorage.setItem(ROLE_KEY, role);
    }
  },

  clear(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(TENANT_ID_KEY);
    sessionStorage.removeItem(ROLE_KEY);
    sessionStorage.removeItem(USER_NAME_KEY);
    sessionStorage.removeItem(DISPLAY_NAME_KEY);
    sessionStorage.removeItem(COMPANY_NAME_KEY);
  },

  isAuthenticated(): boolean {
    return Boolean(this.getAccessToken());
  },
};

/** Map role codes to Main-screen friendly labels. */
export function formatRoleLabel(role: string | null): string {
  if (!role) return "User";
  switch (role.toUpperCase()) {
    case "SUPER_ADMIN":
      return "Super Admin";
    case "TENANT_ADMIN":
      return "Tenant Admin";
    case "TENANT_STAFF":
      return "Tenant Staff";
    default:
      return role
        .split(/[_\s]+/)
        .map((p) => p.charAt(0) + p.slice(1).toLowerCase())
        .join(" ");
  }
}

/** Initials for avatar circle (e.g. "Subhan Khan" → "SK"). */
export function initialsFromName(name: string | null | undefined): string {
  if (!name?.trim()) return "U";
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Live Users.UserName from session — no seed aliases (Platform Admin / Demo Admin). */
export function formatDisplayName(name: string | null | undefined): string {
  const raw = name?.trim();
  return raw && raw.length > 0 ? raw : "User";
}
