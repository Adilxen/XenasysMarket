import axios, {
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from "axios";
import { sessionStore } from "@/lib/auth/session";

/**
 * ==========================================================================================
 * WHAT: Shared Axios API client with JWT/Bearer injection and 401 → /login redirect.
 * WHY: Login (company_id INT) and authenticated calls share one base URL and auth headers.
 * EXAMPLE: apiPost("/api/auth/login", { userName, password, company_id: 12 }) via apiClient.
 * ==========================================================================================
 */
function createApiClient(): AxiosInstance {
  const baseURL =
    process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5077";

  const client = axios.create({
    baseURL,
    timeout: 10_000,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
  });

  client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = sessionStore.getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    // Optional diagnostic header — never used for authorization/isolation.
    const tenantId = sessionStore.getTenantId();
    if (tenantId) {
      config.headers["X-Tenant-Id"] = tenantId;
    }

    return config;
  });

  client.interceptors.response.use(
    (response) => response,
    (error) => {
      if (error.response?.status === 401 && typeof window !== "undefined") {
        sessionStore.clear();
        if (!window.location.pathname.startsWith("/login")) {
          window.location.assign("/login");
        }
      }
      return Promise.reject(error);
    },
  );

  return client;
}

export const apiClient = createApiClient();

export async function apiGet<T>(
  url: string,
  config?: AxiosRequestConfig,
): Promise<T> {
  const { data } = await apiClient.get<T>(url, config);
  return data;
}

export async function apiPost<TRequest, TResponse>(
  url: string,
  body: TRequest,
  config?: AxiosRequestConfig,
): Promise<TResponse> {
  const { data } = await apiClient.post<TResponse>(url, body, config);
  return data;
}

export async function apiPut<TRequest, TResponse>(
  url: string,
  body: TRequest,
  config?: AxiosRequestConfig,
): Promise<TResponse> {
  const { data } = await apiClient.put<TResponse>(url, body, config);
  return data;
}

export async function apiDelete<T>(
  url: string,
  config?: AxiosRequestConfig,
): Promise<T> {
  const { data } = await apiClient.delete<T>(url, config);
  return data;
}
