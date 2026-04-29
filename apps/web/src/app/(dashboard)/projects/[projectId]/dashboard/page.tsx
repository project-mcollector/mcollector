"use client";

import { useEffect, useState } from "react";
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

const BASE_URL =
  process.env.NEXT_PUBLIC_ANALYTICS_URL || "http://localhost:5002";

function analyticsBase(projectId: string | string[]) {
  return `${BASE_URL}/api/v1/projects/${projectId}/analytics`;
}

function getDateRange() {
  const now = Date.now();
  return {
    to: new Date(now).toISOString(),
    from: new Date(now - 30 * 24 * 60 * 60 * 1000).toISOString(),
  };
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

type EventName = string;

export default function DashboardPage() {
  const router = useRouter();
  const { projectId } = useParams<{ projectId: string }>();

  const [overview, setOverview] = useState<Overview | null>(null);
  const [eventsTimeseries, setEventsTimeseries] = useState<TimeseriesPoint[]>(
    [],
  );
  const [usersTimeseries, setUsersTimeseries] = useState<TimeseriesPoint[]>([]);
  const [eventNames, setEventNames] = useState<EventName[]>([]);
  const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
  const [selectedEventTimeseries, setSelectedEventTimeseries] = useState<
    TimeseriesPoint[]
  >([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
      return;
    }

    const { to, from } = getDateRange();
    const headers = { Authorization: `Bearer ${token}` };
    const base = analyticsBase(projectId);

    Promise.all([
      fetch(`${base}/overview?from=${from}&to=${to}`, { headers }).then((r) =>
        r.json(),
      ),
      fetch(`${base}/events/timeseries?from=${from}&to=${to}&interval=day`, {
        headers,
      }).then((r) => r.json()),
      fetch(`${base}/users/timeseries?from=${from}&to=${to}&interval=day`, {
        headers,
      }).then((r) => r.json()),
      fetch(`${base}/events`, { headers }).then((r) => r.json()),
    ]).then(([overviewData, eventsData, usersData, eventNamesData]) => {
      setOverview(overviewData);
      setEventsTimeseries(eventsData);
      setUsersTimeseries(usersData);
      setEventNames(eventNamesData);
      setLoading(false);
    });
  }, [projectId, router]);

  function handleEventClick(eventName: string) {
    const token = localStorage.getItem("token");
    if (!token) return;

    if (selectedEvent === eventName) {
      setSelectedEvent(null);
      setSelectedEventTimeseries([]);
      return;
    }

    const { to, from } = getDateRange();
    const headers = { Authorization: `Bearer ${token}` };
    const base = analyticsBase(projectId);

    fetch(
      `${base}/events/timeseries?from=${from}&to=${to}&interval=day&eventName=${eventName}`,
      { headers },
    )
      .then((r) => r.json())
      .then((data) => {
        setSelectedEvent(eventName);
        setSelectedEventTimeseries(data);
      });
  }

  if (loading) return <p className={styles.emptyState}>Загрузка...</p>;
  if (!overview) return <p className={styles.emptyState}>Нет данных</p>;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Дашборд</h1>
        </div>
        <button
          className={styles.buttonOutline}
          onClick={() => router.push("/projects")}
        >
          ← Мои проекты
        </button>
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
            <XAxis dataKey="timestamp" />
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
            {eventNames.map((event) => (
              <>
                <tr key={event}>
                  <td>{event}</td>
                  <td></td>
                  <td>
                    <button
                      className={styles.buttonSmall}
                      onClick={() => handleEventClick(event)}
                    >
                      {selectedEvent === event ? "Скрыть" : "График"}
                    </button>
                  </td>
                </tr>

                {selectedEvent === event && (
                  <tr key={`${event}-chart`}>
                    <td colSpan={3}>
                      <ResponsiveContainer width="100%" height={200}>
                        <LineChart data={selectedEventTimeseries}>
                          <XAxis dataKey="timestamp" />
                          <YAxis />
                          <Tooltip />
                          <Line
                            type="monotone"
                            dataKey="count"
                            stroke="#8884d8"
                          />
                        </LineChart>
                      </ResponsiveContainer>
                    </td>
                  </tr>
                )}
              </>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.chartSection}>
        <h2 className={styles.chartTitle}>Пользователи по дням</h2>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={usersTimeseries}>
            <XAxis dataKey="timestamp" />
            <YAxis />
            <Tooltip />
            <Line type="monotone" dataKey="count" stroke="#82ca9d" />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
