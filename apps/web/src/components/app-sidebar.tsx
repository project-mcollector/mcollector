"use client";

import { useEffect, useState } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
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

const navItems = [
  { title: "Projects", url: "/projects", icon: FolderOpen },
  { title: "Settings", url: "/settings", icon: Settings },
];

export function AppSidebar() {
  const router = useRouter();
  const pathname = usePathname();
  const [user, setUser] = useState<UserProfile | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);

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
          title="Delete account?"
          message="All your projects and data will be permanently deleted. This cannot be undone."
          confirmLabel={deleting ? "Deleting…" : "Delete account"}
          danger
          onConfirm={deleteAccount}
          onCancel={() => setDeleteConfirm(false)}
        />
      )}
    </>
  );
}
