"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { BASE_URL } from "@/lib/constants";
import styles from "../login.module.css";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    document.title = "MCollector — Сброс пароля";
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await fetch(`${BASE_URL}/api/auth/forgot-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      setDone(true);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>Сброс пароля</p>

        {done ? (
          <>
            <p style={{ fontSize: 14, color: "#3f3f46", textAlign: "center", lineHeight: 1.6 }}>
              Если аккаунт с адресом <strong>{email}</strong> существует, мы отправили письмо со ссылкой для сброса пароля.
            </p>
            <p className={styles.linkText}>
              <Link href="/login" className={styles.link}>Войти</Link>
            </p>
          </>
        ) : (
          <form onSubmit={handleSubmit} className={styles.form}>
            <div>
              <label className={styles.label}>Email</label>
              <input
                type="email"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
                autoComplete="email"
                disabled={loading}
                className={styles.input}
              />
            </div>

            <button type="submit" disabled={loading} className={styles.button}>
              {loading ? "Отправка..." : "Отправить ссылку"}
            </button>
            <p className={styles.linkText}>
              <Link href="/login" className={styles.link}>Вернуться ко входу</Link>
            </p>
          </form>
        )}
      </div>
    </div>
  );
}
