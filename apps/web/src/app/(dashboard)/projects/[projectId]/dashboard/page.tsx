"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
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

const BASE_URL =
  process.env.NEXT_PUBLIC_ANALYTICS_URL || "http://localhost:5002";

function analyticsBase(projectId: string | string[]) {
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
  return new Date(iso).toLocaleDateString("ru-RU", { month: "short", day: "numeric" });
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

export default function DashboardPage() {
  const router = useRouter();
  const { projectId } = useParams<{ projectId: string }>();

  const [days, setDays] = useState(30);
  const [overview, setOverview] = useState<Overview | null>(null);
  const [eventsTimeseries, setEventsTimeseries] = useState<TimeseriesPoint[]>([]);
  const [usersTimeseries, setUsersTimeseries] = useState<TimeseriesPoint[]>([]);
  const [eventCounts, setEventCounts] = useState<EventCount[]>([]);
  const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
  const [selectedEventTimeseries, setSelectedEventTimeseries] = useState<TimeseriesPoint[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchData = useCallback(() => {
    const { to, from } = getDateRange(days);
    const base = analyticsBase(projectId);

    setLoading(true);
    setSelectedEvent(null);
    setSelectedEventTimeseries([]);

    Promise.all([
      authFetch(`${base}/overview?from=${from}&to=${to}`, router).then((r) => r.json()),
      authFetch(`${base}/events/timeseries?from=${from}&to=${to}&interval=day`, router).then((r) => r.json()),
      authFetch(`${base}/users/timeseries?from=${from}&to=${to}&interval=day`, router).then((r) => r.json()),
      authFetch(`${base}/events/counts?from=${from}&to=${to}`, router).then((r) => r.json()),
    ])
      .then(([overviewData, eventsData, usersData, countsData]) => {
        setOverview(overviewData);
        setEventsTimeseries(eventsData);
        setUsersTimeseries(usersData);
        setEventCounts(countsData);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [projectId, days, router]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
      return;
    }
    fetchData();
  }, [fetchData, router]);

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

  if (loading) return (
    <div className={styles.page}>
      <div className={styles.statsGrid}>
        {[...Array(3)].map((_, i) => <div key={i} className={styles.skeletonCard} />)}
      </div>
    </div>
  );

  if (!overview) return <p className={styles.emptyState}>Нет данных</p>;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Дашборд</h1>
        </div>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
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
          <button className={styles.buttonOutline} onClick={fetchData}>↻ Обновить</button>
          <button className={styles.buttonOutline} onClick={() => router.push("/projects")}>
            ← Мои проекты
          </button>
          <button className={styles.buttonOutline} onClick={() => { localStorage.removeItem("token"); router.push("/login"); }}>
            Выйти
          </button>
        </div>
      </div>

      <div className={styles.statsGrid}>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>События</p>
          <p className={styles.statValue}>{overview.totalEvents}</p>
        </div>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>Пользователи</p>
          <p className={styles.statValue}>{overview.uniqueUsers}</p>
        </div>
        <div className={styles.statCard}>
          <p className={styles.statLabel}>Просмотры страниц</p>
          <p className={styles.statValue}>{overview.pageViews}</p>
        </div>
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>События по дням</h2>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={eventsTimeseries}>
            <XAxis dataKey="timestamp" tickFormatter={formatDate} />
            <YAxis />
            <Tooltip />
            <Line type="monotone" dataKey="count" stroke="#8884d8" />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>События</h2>
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
                  <td>{event.count}</td>
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
                          <Tooltip />
                          <Line type="monotone" dataKey="count" stroke="#8884d8" />
                        </LineChart>
                      </ResponsiveContainer>
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>Пользователи по дням</h2>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={usersTimeseries}>
            <XAxis dataKey="timestamp" tickFormatter={formatDate} />
            <YAxis />
            <Tooltip />
            <Line type="monotone" dataKey="count" stroke="#82ca9d" />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
