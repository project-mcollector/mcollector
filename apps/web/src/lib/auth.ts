import { AppRouterInstance } from "next/dist/shared/lib/app-router-context.shared-runtime";
import { BASE_URL } from "./constants";

let refreshPromise: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  const refreshToken = localStorage.getItem("refreshToken");
  if (!refreshToken) return null;

  try {
    const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });

    if (!res.ok) {
      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      return null;
    }

    const data = await res.json();
    localStorage.setItem("token", data.accessToken);
    localStorage.setItem("refreshToken", data.refreshToken);
    return data.accessToken as string;
  } catch {
    localStorage.removeItem("token");
    localStorage.removeItem("refreshToken");
    return null;
  }
}

export function authFetch(
  url: string,
  router: AppRouterInstance,
  options: RequestInit = {}
): Promise<Response> {
  const token = localStorage.getItem("token");
  return fetch(url, {
    ...options,
    headers: { ...options.headers, Authorization: `Bearer ${token}` },
  }).then(async (res) => {
    if (res.status === 401) {
      if (!refreshPromise) {
        refreshPromise = tryRefresh().finally(() => {
          refreshPromise = null;
        });
      }
      const newToken = await refreshPromise;
      if (!newToken) {
        router.push("/login");
        throw new Error("Unauthorized");
      }
      return fetch(url, {
        ...options,
        headers: { ...options.headers, Authorization: `Bearer ${newToken}` },
      }).then((retryRes) => {
        if (!retryRes.ok) throw new Error(`Request failed with status ${retryRes.status}`);
        return retryRes;
      });
    }
    if (!res.ok) {
      throw new Error(`Request failed with status ${res.status}`);
    }
    return res;
  });
}

export async function logout(router: AppRouterInstance): Promise<void> {
  const refreshToken = localStorage.getItem("refreshToken");
  if (refreshToken) {
    try {
      await fetch(`${BASE_URL}/api/auth/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {}
  }
  localStorage.removeItem("token");
  localStorage.removeItem("refreshToken");
  router.push("/login");
}
