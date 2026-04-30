import { AppRouterInstance } from "next/dist/shared/lib/app-router-context.shared-runtime";

export function authFetch(
  url: string,
  router: AppRouterInstance,
  options: RequestInit = {}
): Promise<Response> {
  const token = localStorage.getItem("token");
  return fetch(url, {
    ...options,
    headers: { ...options.headers, Authorization: `Bearer ${token}` },
  }).then((res) => {
    if (res.status === 401) {
      localStorage.removeItem("token");
      router.push("/login");
      throw new Error("Unauthorized");
    }
    return res;
  });
}
