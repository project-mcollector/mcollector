'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'

export default function LoginPage() {
  const router = useRouter()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()

    const res = await fetch('http://localhost:PORT/api/auth/login', {
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
    <div>
      <h1>Вход</h1>
      {error && <p>{error}</p>}
      <form onSubmit={handleSubmit}>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
          />
        </label>
        <label>
          Password
          <input
          type="password"
          value={password}
          onChange={e => setPassword(e.target.value)}
        />
        </label>
        <label>
          <button type="submit">Войти</button>
        </label>

      </form>
    </div>
  )
}
