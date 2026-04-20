'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import styles from '../login.module.css'

const BASE_URL = 'http://localhost:PORT'

export default function LoginPage() {
  const router = useRouter()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()

    const res = await fetch(`${BASE_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    })

    if (!res.ok) {
      setError('Неверный email или пароль')
      return
    }

    const data = await res.json()
    localStorage.setItem('token', data.token)
    router.push('/projects')
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>

        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>Аналитика для вашего сайта</p>

        <form onSubmit={handleSubmit} className={styles.form}>

          {error && (
            <div className={styles.error}>{error}</div>
          )}

          <div>
            <label className={styles.label}>Email</label>
            <input
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>Пароль</label>
            <input
              type="password"
              placeholder="••••••••"
              value={password}
              onChange={e => setPassword(e.target.value)}
              className={styles.input}
            />
          </div>

          <button type="submit" className={styles.button}>
            Войти
          </button>
          <p className={styles.linkText}>
            Нет аккаунта?{' '}
            <Link href="/register" className={styles.link}>
              Зарегистрироваться
            </Link>
          </p>
        </form>
      </div>
    </div>
  )
}
