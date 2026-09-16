import { redirect } from "next/navigation";
import { Brand } from "@/components/brand";
import { LogoutButton } from "@/components/logout-button";
import { PortalNav } from "@/components/portal-nav";
import { getCurrentUser } from "@/lib/auth";

export default async function PortalLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const user = await getCurrentUser();
  if (!user) {
    redirect("/login");
  }

  const displayName = user.name?.split("@")[0] || "operador";

  return (
    <div className="portal-shell">
      <aside className="sidebar">
        <Brand />

        <PortalNav />

        <div className="sidebar-footer">
          <div className="user-summary">
            <span className="avatar">{displayName.slice(0, 2).toUpperCase()}</span>
            <span>
              <strong>{displayName}</strong>
              <small>{user.roles.join(" · ")}</small>
            </span>
          </div>
          <LogoutButton />
        </div>
      </aside>

      <div className="portal-content">
        <header className="mobile-header">
          <Brand compact />
          <div className="mobile-user">
            <span>{displayName}</span>
            <LogoutButton />
          </div>
        </header>
        <PortalNav mobile />
        {children}
      </div>
    </div>
  );
}
