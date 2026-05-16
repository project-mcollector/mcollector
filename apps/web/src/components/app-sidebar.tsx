"use client";

import { useEffect, useState } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { FolderOpen, Settings, LogOut, Trash2, ChevronsUpDown, GalleryVerticalEnd } from "lucide-react";
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { authFetch, logout } from "@/lib/auth";
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

function UserAvatar({ email }: { email: string | null }) {
  const initial = email?.[0]?.toUpperCase() ?? "?";
  return (
    <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-zinc-900 text-xs font-semibold text-white">
      {initial}
    </div>
  );
}

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

  const displayName = user?.userName ?? user?.email ?? "…";

  return (
    <>
      <Sidebar collapsible="icon">
        <SidebarHeader>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton size="lg" asChild>
                <Link href="/projects">
                  <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-zinc-900 text-white">
                    <GalleryVerticalEnd className="size-4" />
                  </div>
                  <span className="truncate font-semibold">MCollector</span>
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
                    <SidebarMenuButton asChild isActive={pathname.startsWith(item.url)}>
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
          <SidebarMenu>
            <SidebarMenuItem>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <SidebarMenuButton size="lg">
                    <UserAvatar email={user?.email ?? null} />
                    <div className="grid flex-1 text-left text-sm leading-tight">
                      <span className="truncate font-semibold">{displayName}</span>
                      {user?.userName && user.email && (
                        <span className="truncate text-xs text-muted-foreground">
                          {user.email}
                        </span>
                      )}
                    </div>
                    <ChevronsUpDown className="ml-auto size-4" />
                  </SidebarMenuButton>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  className="w-[--radix-dropdown-menu-trigger-width] min-w-56 rounded-lg"
                  side="top"
                  align="end"
                  sideOffset={4}
                >
                  <DropdownMenuLabel className="p-0 font-normal">
                    <div className="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
                      <UserAvatar email={user?.email ?? null} />
                      <div className="grid flex-1 text-left text-sm leading-tight">
                        <span className="truncate font-semibold">{displayName}</span>
                        {user?.email && (
                          <span className="truncate text-xs text-muted-foreground">
                            {user.email}
                          </span>
                        )}
                      </div>
                    </div>
                  </DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => logout(router)}>
                    <LogOut className="size-4" />
                    Log out
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    variant="destructive"
                    onClick={() => setDeleteConfirm(true)}
                  >
                    <Trash2 className="size-4" />
                    Delete account
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </SidebarMenuItem>
          </SidebarMenu>
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