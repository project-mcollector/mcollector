'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import styles from '../login.module.css'

const BASE_URL = 'http://localhost:5003'
export default function RegisterPage() {
  const router = useRouter()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [organizationName, setOrganizationName] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')

    if (password !== confirmPassword) {
      setError('Пароли не совпадают')
      return
    }

    if (password.length < 8) {
      setError('Пароль должен быть не менее 8 символов')
      return
    }

    setLoading(true)

    try {
      const res = await fetch(`${BASE_URL}/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email,
          password,
          organizationName
        })
      })

      if (!res.ok) {
        let msg = 'Ошибка регистрации'
        try {
          const errData = await res.json()
          if (errData.errors) {
            const firstKey = Object.keys(errData.errors)[0]
            if (firstKey) msg = errData.errors[firstKey][0]
          } else if (errData.title) {
            msg = errData.title
          }
        } catch { } // if not json

        setError(msg)
        return
      }

      // Successful registration
      const loginRes = await fetch(`${BASE_URL}/Auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      })

      if (loginRes.ok) {
        const data = await loginRes.json()
        localStorage.setItem('token', data.accessToken)
        router.push('/projects')
      } else {
        router.push('/login')
      }
    } catch (err) {
      setError('Ошибка соединения с сервером')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>

        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>Создайте аккаунт</p>

        <form onSubmit={handleSubmit} className={styles.form}>

          {error && <div className={styles.error}>{error}</div>}

          <div>
            <label className={styles.label}>Email</label>
            <input
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>Название организации</label>
            <input
              type="text"
              placeholder="Моя компания"
              value={organizationName}
              onChange={e => setOrganizationName(e.target.value)}
              required
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
              required
              minLength={8}
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>Подтвердите пароль</label>
            <input
              type="password"
              placeholder="••••••••"
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
              required
              className={styles.input}
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className={styles.button}
          >
            {loading ? 'Регистрация...' : 'Зарегистрироваться'}
          </button>

        </form>

        <p className={styles.linkText}>
          Уже есть аккаунт?{' '}
          <Link href="/login" className={styles.link}>
            Войти
          </Link>
        </p>

      </div>
    </div>
  )
}
