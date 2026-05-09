"use client";

import { Fragment, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import styles from "../../../dashboard.module.css";
import { analytics } from "@mcollector/sdk";
import { authFetch } from "@/lib/auth";
import { BASE_URL } from "@/lib/constants";
import ConfirmModal from "@/components/ConfirmModal";

function analyticsBase(projectId: string) {
  return `${BASE_URL}/api/v1/projects/${projectId}/analytics`;
}

function getDateRange(days: number) {
  const now = Date.now();
  return {
    to: new Date(now).toISOString(),
    from: new Date(now - days * 24 * 60 * 60 * 1000).toISOString(),
  };
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("ru-RU", {
    month: "short",
    day: "numeric",
  });
}

function formatNumber(n: number) {
  return n.toLocaleString("ru-RU");
}

type Overview = {
  totalEvents: number;
  uniqueUsers: number;
  pageViews: number;
};

type TimeseriesPoint = {
  timestamp: string;
  count: number;
};

type EventCount = {
  name: string;
  count: number;
};

type Project = {
  id: string;
  name: string;
};

export default function DashboardPage() {
  const router = useRouter();
  const params = useParams();
  const projectId = params.projectId as string;

  const [days, setDays] = useState(30);
  const [refreshKey, setRefreshKey] = useState(0);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const [project, setProject] = useState<Project | null>(null);
  const [overview, setOverview] = useState<Overview | null>(null);
  const [eventsTimeseries, setEventsTimeseries] = useState<TimeseriesPoint[]>([]);
  const [usersTimeseries, setUsersTimeseries] = useState<TimeseriesPoint[]>([]);
  const [eventCounts, setEventCounts] = useState<EventCount[]>([]);
  const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
  const [selectedEventTimeseries, setSelectedEventTimeseries] = useState<TimeseriesPoint[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [renameOpen, setRenameOpen] = useState(false);
  const [renameName, setRenameName] = useState("");
  const [renaming, setRenaming] = useState(false);
  const trackedOpenProjectId = useRef<string | null>(null);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
    }
  }, [router]);

  useEffect(() => {
    authFetch(`${BASE_URL}/api/projects/${projectId}`, router)
      .then((r) => r.json())
      .then((data) => {
        setProject(data);
        document.title = `MCollector — ${data.name}`;
        if (trackedOpenProjectId.current !== data.id) {
          trackedOpenProjectId.current = data.id;
          analytics.track("project_opened", {
            projectId: data.id,
            projectName: data.name,
            source: "dashboard_page",
          });
        }
      })
      .catch(() => {
        setLoadError("Не удалось загрузить данные проекта.");
      });
  }, [projectId, router]);

  useEffect(() => {
    if (project) {
      document.title = `MCollector — ${project.name}`;
    }
  }, [project]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) return;

    let cancelled = false;

    async function load() {
      const { to, from } = getDateRange(days);
      const base = analyticsBase(projectId);

      setLoading(true);
      setLoadError(null);
      setSelectedEvent(null);
      setSelectedEventTimeseries([]);

      try {
        const [overviewData, eventsData, usersData, countsData] =
          await Promise.all([
            authFetch(`${base}/overview?from=${from}&to=${to}`, router).then((r) => r.json()),
            authFetch(`${base}/events/timeseries?from=${from}&to=${to}&interval=day`, router).then((r) => r.json()),
            authFetch(`${base}/users/timeseries?from=${from}&to=${to}&interval=day`, router).then((r) => r.json()),
            authFetch(`${base}/events/counts?from=${from}&to=${to}`, router).then((r) => r.json()),
          ]);

        if (cancelled) return;

        setOverview(overviewData);
        setEventsTimeseries(eventsData);
        setUsersTimeseries(usersData);
        setEventCounts(countsData);
        setLastUpdated(new Date());
      } catch (err) {
        if (!cancelled && !(err instanceof Error && err.message === "Unauthorized")) {
          setLoadError("Не удалось загрузить данные. Попробуйте обновить страницу.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, [projectId, days, refreshKey, router]);

  function handleEventClick(eventName: string) {
    if (selectedEvent === eventName) {
      setSelectedEvent(null);
      setSelectedEventTimeseries([]);
      return;
    }

    const { to, from } = getDateRange(days);
    const base = analyticsBase(projectId);

    authFetch(
      `${base}/events/timeseries?from=${from}&to=${to}&interval=day&eventName=${eventName}`,
      router,
    )
      .then((r) => r.json())
      .then((data) => {
        setSelectedEvent(eventName);
        setSelectedEventTimeseries(data);
      })
      .catch(() => {});
  }

  async function confirmRename() {
    if (!renameName.trim() || renaming) return;
    setRenaming(true);
    try {
      const res = await authFetch(`${BASE_URL}/api/projects/${projectId}`, router, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: renameName.trim() }),
      });
      const updated = await res.json();
      setProject(updated);
      document.title = `MCollector — ${updated.name}`;
      setRenameOpen(false);
    } catch {
    } finally {
      setRenaming(false);
    }
  }

  async function confirmDelete() {
    setDeleting(true);
    try {
      await authFetch(`${BASE_URL}/api/projects/${projectId}`, router, {
        method: "DELETE",
      });
      router.push("/projects");
    } catch {
    } finally {
      setDeleting(false);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipFormatter = (value: any, name: any) => [
    typeof value === "number" ? formatNumber(value) : String(value ?? ""),
    name,
  ];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipLabelFormatter = (label: any) =>
    typeof label === "string" ? formatDate(label) : String(label ?? "");

  const nav = (
    <div className={styles.dashboardNav}>
      <button className={styles.navBackBtn} onClick={() => router.push("/projects")}>
        ← Проекты
      </button>
      {project && (
        <>
          <span className={styles.navSeparator}>/</span>
          <span className={styles.navCurrent}>{project.name}</span>
        </>
      )}
    </div>
  );

  if (loading)
    return (
      <div className={styles.page}>
        {nav}
        <div className={styles.statsGrid}>
          {[...Array(3)].map((_, i) => (
            <div key={i} className={styles.skeletonCard} />
          ))}
        </div>
        <div className={styles.skeletonChart} />
        <div className={styles.skeletonChart} />
      </div>
    );

  if (loadError)
    return (
      <div className={styles.page}>
        {nav}
        <p className={styles.emptyState}>{loadError}</p>
      </div>
    );

  if (!overview)
    return (
      <div className={styles.page}>
        {nav}
        <p className={styles.emptyState}>Нет данных за выбранный период</p>
      </div>
    );

  return (
    <div className={styles.page}>
      {nav}

      <div className={styles.header}>
        <div>
          <div className={styles.projectNameRow}>
            <h1 className={styles.title}>{project?.name ?? "Дашборд"}</h1>
            {project && (
              <button
                className={styles.renameIcon}
                title="Переименовать"
                onClick={() => { setRenameName(project.name); setRenameOpen(true); }}
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              </button>
            )}
          </div>
          {lastUpdated && (
            <p className={styles.subtitle}>
              Обновлено в {lastUpdated.toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })}
            </p>
          )}
        </div>

        <div className={styles.dashboardControls}>
          <div className={styles.dateRange}>
            {[7, 30, 90].map((d) => (
              <button
                key={d}
                className={days === d ? styles.dateRangeActive : styles.dateRangeBtn}
                onClick={() => setDays(d)}
              >
                {d}д
              </button>
            ))}
          </div>

          <button
            className={styles.buttonOutline}
            onClick={() => setRefreshKey((k) => k + 1)}
          >
            ↻ Обновить
          </button>

          <button
            className={styles.deleteButtonOutline}
            onClick={() => setDeleteConfirm(true)}
            disabled={deleting}
          >
            Удалить проект
          </button>
        </div>
      </div>

      <div className={styles.statsGrid}>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>События</p>
          <p className={styles.statValue}>{formatNumber(overview.totalEvents)}</p>
        </div>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>Пользователи</p>
          <p className={styles.statValue}>{formatNumber(overview.uniqueUsers)}</p>
        </div>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>Просмотры страниц</p>
          <p className={styles.statValue}>{formatNumber(overview.pageViews)}</p>
        </div>
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>События по дням</h2>
        {eventsTimeseries.length === 0 ? (
          <p className={styles.chartEmpty}>Нет событий за выбранный период</p>
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={eventsTimeseries}>
              <XAxis dataKey="timestamp" tickFormatter={formatDate} />
              <YAxis allowDecimals={false} />
              <Tooltip formatter={tooltipFormatter} labelFormatter={tooltipLabelFormatter} />
              <Line type="monotone" dataKey="count" name="События" stroke="#8884d8" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>Топ событий</h2>
        {eventCounts.length === 0 ? (
          <p className={styles.chartEmpty}>Нет данных</p>
        ) : (
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Название</th>
                <th>Количество</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {eventCounts.map((event) => (
                <Fragment key={event.name}>
                  <tr>
                    <td>{event.name}</td>
                    <td>{formatNumber(event.count)}</td>
                    <td>
                      <button
                        className={styles.buttonSmall}
                        onClick={() => handleEventClick(event.name)}
                      >
                        {selectedEvent === event.name ? "Скрыть" : "График"}
                      </button>
                    </td>
                  </tr>

                  {selectedEvent === event.name && (
                    <tr>
                      <td colSpan={3}>
                        <ResponsiveContainer width="100%" height={200}>
                          <LineChart data={selectedEventTimeseries}>
                            <XAxis dataKey="timestamp" tickFormatter={formatDate} />
                            <YAxis allowDecimals={false} />
                            <Tooltip formatter={tooltipFormatter} labelFormatter={tooltipLabelFormatter} />
                            <Line type="monotone" dataKey="count" name={event.name} stroke="#8884d8" dot={false} />
                          </LineChart>
                        </ResponsiveContainer>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>Пользователи по дням</h2>
        {usersTimeseries.length === 0 ? (
          <p className={styles.chartEmpty}>Нет данных за выбранный период</p>
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={usersTimeseries}>
              <XAxis dataKey="timestamp" tickFormatter={formatDate} />
              <YAxis allowDecimals={false} />
              <Tooltip formatter={tooltipFormatter} labelFormatter={tooltipLabelFormatter} />
              <Line type="monotone" dataKey="count" name="Пользователи" stroke="#82ca9d" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>

      {renameOpen && (
        <div className={styles.modalOverlay} onClick={() => setRenameOpen(false)}>
          <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
            <h2 className={styles.modalTitle}>Переименовать проект</h2>
            <label className={styles.label}>Новое название</label>
            <input
              type="text"
              value={renameName}
              onChange={(e) => setRenameName(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && confirmRename()}
              className={styles.input}
              autoFocus
              style={{ marginBottom: 20 }}
            />
            <div className={styles.modalButtons}>
              <button className={styles.button} onClick={confirmRename} disabled={renaming || !renameName.trim()}>
                {renaming ? "Сохранение..." : "Сохранить"}
              </button>
              <button className={styles.buttonOutline} onClick={() => setRenameOpen(false)}>
                Отмена
              </button>
            </div>
          </div>
        </div>
      )}

      {deleteConfirm && (
        <ConfirmModal
          title="Удалить проект?"
          message={`Проект "${project?.name}" и все его данные будут удалены. Это действие необратимо.`}
          confirmLabel={deleting ? "Удаление..." : "Удалить"}
          danger
          onConfirm={confirmDelete}
          onCancel={() => setDeleteConfirm(false)}
        />
      )}
    </div>
  );
}
