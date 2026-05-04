"use client";

import { Fragment, useEffect, useState } from "react";
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
import { authFetch } from "@/lib/auth";

const ANALYTICS_URL =
  process.env.NEXT_PUBLIC_ANALYTICS_URL || "http://localhost:5002";
const IDENTITY_URL =
  process.env.NEXT_PUBLIC_IDENTITY_URL || "http://localhost:5003";

function analyticsBase(projectId: string) {
  return `${ANALYTICS_URL}/api/v1/projects/${projectId}/analytics`;
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

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
    }
  }, [router]);

  useEffect(() => {
    authFetch(`${IDENTITY_URL}/api/projects/${projectId}`, router)
      .then((r) => r.json())
      .then((data) => {
        setProject(data);
        document.title = `MCollector — ${data.name}`;
      })
      .catch(() => {});
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
      } catch {
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

  function logout() {
    localStorage.removeItem("token");
    router.push("/login");
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipFormatter = (value: any, name: any) => [
    typeof value === "number" ? formatNumber(value) : String(value ?? ""),
    name,
  ];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const tooltipLabelFormatter = (label: any) =>
    typeof label === "string" ? formatDate(label) : String(label ?? "");

  if (loading)
    return (
      <div className={styles.page}>
        <div className={styles.dashboardNav}>
          <button className={styles.navBackBtn} onClick={() => router.push("/projects")}>
            ← Проекты
          </button>
        </div>
        <div className={styles.statsGrid}>
          {[...Array(3)].map((_, i) => (
            <div key={i} className={styles.skeletonCard} />
          ))}
        </div>
        <div className={styles.skeletonChart} />
        <div className={styles.skeletonChart} />
      </div>
    );

  if (!overview)
    return (
      <div className={styles.page}>
        <div className={styles.dashboardNav}>
          <button className={styles.navBackBtn} onClick={() => router.push("/projects")}>
            ← Проекты
          </button>
        </div>
        <p className={styles.emptyState}>Нет данных за выбранный период</p>
      </div>
    );

  return (
    <div className={styles.page}>
      <div className={styles.dashboardNav}>
        <button className={styles.navBackBtn} onClick={() => router.push("/projects")}>
          ← Проекты
        </button>
        <span className={styles.navSeparator}>/</span>
        <span className={styles.navCurrent}>{project?.name ?? "..."}</span>
      </div>

      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>{project?.name ?? "Дашборд"}</h1>
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

          <button className={styles.buttonOutline} onClick={logout}>
            Выйти
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
              <YAxis />
              <Tooltip formatter={tooltipFormatter} labelFormatter={tooltipLabelFormatter} />
              <Line type="monotone" dataKey="count" name="События" stroke="#8884d8" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>События</h2>
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
                            <YAxis />
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
              <YAxis />
              <Tooltip formatter={tooltipFormatter} labelFormatter={tooltipLabelFormatter} />
              <Line type="monotone" dataKey="count" name="Пользователи" stroke="#82ca9d" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}
