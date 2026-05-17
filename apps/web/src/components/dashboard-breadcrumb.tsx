"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { usePathname, useRouter, Link } from "@/i18n/navigation";
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

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function DashboardBreadcrumb() {
  const pathname = usePathname();
  const router = useRouter();
  const t = useTranslations("nav");
  const [projectNames, setProjectNames] = useState<Record<string, string>>({});

  const ROUTE_LABELS: Record<string, string> = {
    projects: t("projects"),
    settings: t("settings"),
    dashboard: t("dashboard"),
  };

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
                <BreadcrumbLink asChild>
                  <Link href={crumb.href as Parameters<typeof Link>[0]["href"]}>{crumb.label}</Link>
                </BreadcrumbLink>
              )}
            </BreadcrumbItem>
          </span>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
