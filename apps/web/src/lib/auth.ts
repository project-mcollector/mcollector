import { AppRouterInstance } from "next/dist/shared/lib/app-router-context.shared-runtime";
import { BASE_URL } from "./constants";

let refreshPromise: Promise<string | null> | null = null;

async function readErrorMessage(res: Response): Promise<string> {
  const fallback = `Request failed with status ${res.status}`;
  const contentType = res.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    try {
      const body = (await res.json()) as unknown;
      if (typeof body === "string" && body.trim()) return body;
      if (body && typeof body === "object") {
        const obj = body as { message?: unknown; error?: unknown; description?: unknown };
        if (typeof obj.message === "string" && obj.message.trim()) return obj.message;
        if (typeof obj.error === "string" && obj.error.trim()) return obj.error;
        if (typeof obj.description === "string" && obj.description.trim()) return obj.description;
      }
    } catch {
      // noop, fallback to text/HTTP status
    }
  }

  try {
    const text = (await res.text()).trim();
    return text || fallback;
  } catch {
    return fallback;
  }
}

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

export function isTokenFresh(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split(".")[1]));
    return typeof payload.exp === "number" && payload.exp * 1000 > Date.now();
  } catch {
    return false;
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
      }).then(async (retryRes) => {
        if (!retryRes.ok) throw new Error(await readErrorMessage(retryRes));
        return retryRes;
      });
    }
    if (!res.ok) {
      throw new Error(await readErrorMessage(res));
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
