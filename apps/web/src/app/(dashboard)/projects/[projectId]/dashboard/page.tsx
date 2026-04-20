'use client'

import { useEffect, useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer
} from 'recharts'

const BASE_URL = 'http://localhost:5002'

type Overview = {
  totalEvents: number
  uniqueUsers: number
  pageViews: number
}

type TimeseriesPoint = {
  timestamp: string
  count: number
}

export default function DashboardPage() {
  const router = useRouter()
  const { projectId } = useParams()

  const [overview, setOverview] = useState<Overview | null>(null)
  const [eventsTimeseries, setEventsTimeseries] = useState<TimeseriesPoint[]>([])
  const [usersTimeseries, setUsersTimeseries] = useState<TimeseriesPoint[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) {
      router.push('/login')
      return
    }
    const to = new Date().toISOString()
    const from = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString()
    const headers = { Authorization: `Bearer ${token}` }
    const base = `${BASE_URL}/api/analytics`

    Promise.all([
      fetch(`${base}/overview?projectId=${projectId}&from=${from}&to=${to}`, { headers }).then(r => r.json()),
      fetch(`${base}/events/timeseries?projectId=${projectId}&from=${from}&to=${to}&interval=day`, { headers }).then(r => r.json()),
      fetch(`${base}/users/timeseries?projectId=${projectId}&from=${from}&to=${to}&interval=day`, { headers }).then(r => r.json()),
    ]).then(([overviewData, eventsData, usersData]) => {
      setOverview(overviewData)
      setEventsTimeseries(eventsData)
      setUsersTimeseries(usersData)
      setLoading(false)
    })
  }, [projectId, router])

  if (loading) return <p>Загрузка...</p>
  if (!overview) return <p>Нет данных</p>

  return (
    <div>
      <h1>Дашборд</h1>

      <div>
        <div>
          <p>События</p>
          <p>{overview.totalEvents}</p>
        </div>
        <div>
          <p>Пользователи</p>
          <p>{overview.uniqueUsers}</p>
        </div>
        <div>
          <p>Просмотры страниц</p>
          <p>{overview.pageViews}</p>
        </div>
      </div>

      <h2>События по дням</h2>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={eventsTimeseries}>
          <XAxis dataKey="timestamp" />
          <YAxis />
          <Tooltip />
          <Line type="monotone" dataKey="count" stroke="#8884d8" />
        </LineChart>
      </ResponsiveContainer>

      <h2>Пользователи по дням</h2>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={usersTimeseries}>
          <XAxis dataKey="timestamp" />
          <YAxis />
          <Tooltip />
          <Line type="monotone" dataKey="count" stroke="#82ca9d" />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
