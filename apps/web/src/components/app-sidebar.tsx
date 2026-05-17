"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter, usePathname, Link } from "@/i18n/navigation";
import { FolderOpen, GalleryVerticalEnd, Settings } from "lucide-react";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar";
import { NavUser } from "@/components/nav-user";
import { authFetch } from "@/lib/auth";
import { BASE_URL } from "@/lib/constants";
import ConfirmModal from "@/components/ConfirmModal";

type UserProfile = {
  id: string;
  email: string | null;
  userName: string | null;
};

export function AppSidebar() {
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations("nav");
  const tModal = useTranslations("deleteAccountModal");
  const [user, setUser] = useState<UserProfile | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const navItems = [
    { title: t("projects"), url: "/projects", icon: FolderOpen },
    { title: t("settings"), url: "/settings", icon: Settings },
  ];

  useEffect(() => {
    authFetch(`${BASE_URL}/api/users/me`, router)
      .then((res) => res.json())
      .then(setUser)
      .catch(() => {});
  }, [router]);

  async function deleteAccount() {
    if (deleting) return;
    setDeleting(true);
    try {
      await authFetch(`${BASE_URL}/api/users/me`, router, { method: "DELETE" });
      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      router.push("/login");
    } catch {
      setDeleting(false);
    }
  }

  return (
    <>
      <Sidebar collapsible="icon">
        <SidebarHeader>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton
                size="lg"
                className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                asChild
              >
                <Link href="/projects">
                  <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                    <GalleryVerticalEnd className="size-4" />
                  </div>
                  <div className="grid flex-1 text-left text-sm leading-tight">
                    <span className="truncate font-semibold">MCollector</span>
                    <span className="truncate text-xs text-muted-foreground">Analytics</span>
                  </div>
                </Link>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                {navItems.map((item) => (
                  <SidebarMenuItem key={item.title}>
                    <SidebarMenuButton
                      asChild
                      isActive={pathname.startsWith(item.url)}
                      tooltip={item.title}
                    >
                      <Link href={item.url}>
                        <item.icon />
                        <span>{item.title}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>

        <SidebarFooter>
          <NavUser
            user={user ? { name: user.userName ?? "", email: user.email ?? "" } : null}
            onDeleteAccount={() => setDeleteConfirm(true)}
          />
        </SidebarFooter>
        <SidebarRail />
      </Sidebar>

      {deleteConfirm && (
        <ConfirmModal
          title={tModal("title")}
          message={tModal("message")}
          confirmLabel={deleting ? tModal("deleting") : tModal("confirm")}
          danger
          onConfirm={deleteAccount}
          onCancel={() => setDeleteConfirm(false)}
        />
      )}
    </>
  );
}
