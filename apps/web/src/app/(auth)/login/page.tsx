"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { BASE_URL } from "@/lib/constants";
import styles from "../login.module.css";

function isTokenFresh(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split(".")[1]));
    return typeof payload.exp === "number" && payload.exp * 1000 > Date.now();
  } catch {
    return false;
  }
}

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [emailUnconfirmed, setEmailUnconfirmed] = useState(false);
  const [resendLoading, setResendLoading] = useState(false);
  const [resendDone, setResendDone] = useState(false);
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    document.title = "MCollector — Вход";

    async function checkSession() {
      const token = localStorage.getItem("token");
      if (token && isTokenFresh(token)) {
        router.replace("/projects");
        return;
      }

      const refreshToken = localStorage.getItem("refreshToken");
      if (refreshToken) {
        try {
          const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken }),
          });
          if (res.ok) {
            const data = await res.json();
            localStorage.setItem("token", data.accessToken);
            localStorage.setItem("refreshToken", data.refreshToken);
            router.replace("/projects");
            return;
          }
        } catch {}
      }

      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      setReady(true);
    }

    checkSession();
  }, [router]);

  if (!ready) return null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    setEmailUnconfirmed(false);

    try {
      const res = await fetch(`${BASE_URL}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (res.status === 403) {
        setEmailUnconfirmed(true);
        return;
      }

      if (!res.ok) {
        setError("Неверный email или пароль");
        return;
      }

      const data = await res.json();
      localStorage.setItem("token", data.accessToken);
      localStorage.setItem("refreshToken", data.refreshToken);
      router.push("/projects");
    } catch {
      setError("Не удалось подключиться к серверу");
    } finally {
      setLoading(false);
    }
  }

  async function handleResend() {
    setResendLoading(true);
    try {
      await fetch(`${BASE_URL}/api/auth/resend-confirmation`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      setResendDone(true);
    } finally {
      setResendLoading(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>Аналитика для вашего сайта</p>

        <form onSubmit={handleSubmit} className={styles.form}>
          {error && <div className={styles.error}>{error}</div>}
          {emailUnconfirmed && (
            <div className={styles.error}>
              Подтвердите email перед входом{" "}
              {resendDone ? (
                <span style={{ color: "#16a34a" }}>Письмо отправлено</span>
              ) : (
                <button
                  type="button"
                  onClick={handleResend}
                  disabled={resendLoading}
                  style={{ background: "none", border: "none", cursor: "pointer", padding: 0, textDecoration: "underline" }}
                >
                  {resendLoading ? "Отправка..." : "Отправить снова"}
                </button>
              )}
            </div>
          )}

          <div>
            <label className={styles.label}>Email</label>
            <input
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => { setEmail(e.target.value); setError(""); }}
              required
              autoFocus
              autoComplete="email"
              disabled={loading}
              className={styles.input}
            />
          </div>

          <div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
              <label className={styles.label} style={{ marginBottom: 0 }}>Пароль</label>
              <Link href="/forgot-password" className={styles.link} style={{ fontSize: 13 }}>Забыли пароль?</Link>
            </div>
            <div className={styles.passwordWrapper}>
              <input
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                value={password}
                onChange={(e) => { setPassword(e.target.value); setError(""); }}
                required
                autoComplete="current-password"
                disabled={loading}
                className={styles.input}
              />
              <button type="button" className={styles.passwordToggle} onClick={() => setShowPassword(v => !v)}>
                {showPassword ? (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
                ) : (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                )}
              </button>
            </div>
          </div>

          <button type="submit" disabled={loading} className={styles.button}>
            {loading ? "Вход..." : "Войти"}
          </button>
          <p className={styles.linkText}>
            Нет аккаунта?{" "}
            <Link href="/register" className={styles.link}>
              Зарегистрироваться
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
