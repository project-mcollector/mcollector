"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { authFetch } from "@/lib/auth";
import { BASE_URL } from "@/lib/constants";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";

const ROUTE_LABELS: Record<string, string> = {
  projects: "Проекты",
  settings: "Настройки",
  dashboard: "Дашборд",
};

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function DashboardBreadcrumb() {
  const pathname = usePathname();
  const router = useRouter();
  const [projectNames, setProjectNames] = useState<Record<string, string>>({});

  const segments = pathname.split("/").filter(Boolean);

  useEffect(() => {
    segments.forEach(async (seg) => {
      if (!UUID_RE.test(seg) || projectNames[seg]) return;
      try {
        const res = await authFetch(`${BASE_URL}/api/projects/${seg}`, router);
        const data = (await res.json()) as { name: string };
        setProjectNames((prev) => ({ ...prev, [seg]: data.name }));
      } catch {}
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  const crumbs = segments.map((seg, i) => {
    const isUuid = UUID_RE.test(seg);
    const label = isUuid ? (projectNames[seg] ?? "…") : (ROUTE_LABELS[seg] ?? seg);
    // UUID segments have no dedicated page — link straight to the dashboard sub-route
    const href = isUuid
      ? "/" + segments.slice(0, i + 1).join("/") + "/dashboard"
      : "/" + segments.slice(0, i + 1).join("/");
    return { label, href, isLast: i === segments.length - 1 };
  });

  if (crumbs.length === 0) return null;

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((crumb, i) => (
          <span key={crumb.href} className="flex items-center gap-1.5">
            {i > 0 && <BreadcrumbSeparator className="hidden md:block" />}
            <BreadcrumbItem className={i < crumbs.length - 1 ? "hidden md:block" : ""}>
              {crumb.isLast ? (
                <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink href={crumb.href}>{crumb.label}</BreadcrumbLink>
              )}
            </BreadcrumbItem>
          </span>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
