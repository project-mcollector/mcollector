"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import styles from "../login.module.css";
import { copyToClipboard } from "@/lib/clipboard";

const BASE_URL =
  process.env.NEXT_PUBLIC_IDENTITY_URL || "http://localhost:5003";

export default function RegisterPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [organizationName, setOrganizationName] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);
  const [createdProject, setCreatedProject] = useState<{ id: string; apiKey: string } | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    document.title = "MCollector — Регистрация";
    if (localStorage.getItem("token")) {
      router.replace("/projects");
    } else {
      setReady(true);
    }
  }, [router]);

  if (!ready) return null;

  function validatePassword(pwd: string): string[] {
    const errors = [];
    if (pwd.length < 8) errors.push("Не менее 8 символов");
    if (!/[a-z]/.test(pwd)) errors.push("Строчная буква (a-z)");
    if (!/[A-Z]/.test(pwd)) errors.push("Заглавная буква (A-Z)");
    if (!/[^a-zA-Z0-9]/.test(pwd)) errors.push("Специальный символ (!@#$ и т.д.)");
    return errors;
  }

  const passwordRequirements = [
    { check: password.length >= 8, label: "Не менее 8 символов" },
    { check: /[a-z]/.test(password), label: "Строчная буква (a-z)" },
    { check: /[A-Z]/.test(password), label: "Заглавная буква (A-Z)" },
    { check: /[^a-zA-Z0-9]/.test(password), label: "Специальный символ (!@#$)" },
  ];

  const passwordsMatch = confirmPassword.length > 0 && password === confirmPassword;
  const passwordsMismatch = confirmPassword.length > 0 && password !== confirmPassword;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError("Пароли не совпадают");
      return;
    }

    const passwordErrors = validatePassword(password);
    if (passwordErrors.length > 0) {
      setError("Пароль не соответствует требованиям");
      return;
    }

    setLoading(true);

    try {
      const res = await fetch(`${BASE_URL}/api/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, organizationName }),
      });

      const data = await res.json();

      if (!res.ok) {
        setError(data.message || "Ошибка регистрации");
        return;
      }

      localStorage.setItem("token", data.accessToken);

      const projectRes = await fetch(`${BASE_URL}/api/projects`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${data.accessToken}`,
        },
        body: JSON.stringify({ name: organizationName }),
      });

      const project = await projectRes.json();
      setCreatedProject({ id: project.id, apiKey: project.apiKey });
    } catch {
      setError("Не удалось подключиться к серверу");
    } finally {
      setLoading(false);
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
              onChange={(e) => setEmail(e.target.value)}
              required
              autoFocus
              autoComplete="email"
              disabled={loading}
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>Название организации</label>
            <input
              type="text"
              placeholder="Моя компания"
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              required
              autoComplete="organization"
              disabled={loading}
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>Пароль</label>
            <div className={styles.passwordWrapper}>
              <input
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="new-password"
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
            {password && (
              <div style={{ marginTop: 8, display: "flex", flexDirection: "column", gap: 4 }}>
                {passwordRequirements.map(({ check, label }) => (
                  <div key={label} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: check ? "#16a34a" : "#a1a1aa" }}>
                    <span style={{ fontSize: 14 }}>{check ? "✓" : "○"}</span>
                    {label}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div>
            <label className={styles.label}>Подтвердите пароль</label>
            <div className={styles.passwordWrapper}>
              <input
                type={showConfirmPassword ? "text" : "password"}
                placeholder="••••••••"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                autoComplete="new-password"
                disabled={loading}
                className={styles.input}
              />
              <button type="button" className={styles.passwordToggle} onClick={() => setShowConfirmPassword(v => !v)}>
                {showConfirmPassword ? (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
                ) : (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                )}
              </button>
            </div>
            {confirmPassword && (
              <div style={{ marginTop: 6, fontSize: 12, display: "flex", alignItems: "center", gap: 6, color: passwordsMatch ? "#16a34a" : "#dc2626" }}>
                <span style={{ fontSize: 14 }}>{passwordsMatch ? "✓" : "○"}</span>
                {passwordsMatch ? "Пароли совпадают" : passwordsMismatch ? "Пароли не совпадают" : ""}
              </div>
            )}
          </div>

          <button type="submit" disabled={loading} className={styles.button}>
            {loading ? "Регистрация..." : "Зарегистрироваться"}
          </button>
        </form>

        <p className={styles.linkText}>
          Уже есть аккаунт?{" "}
          <Link href="/login" className={styles.link}>
            Войти
          </Link>
        </p>
      </div>

      {createdProject && (
        <div className={styles.modalOverlay}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitle}>Проект создан!</h2>
            <p className={styles.modalSubtitle}>
              Сохраните ваш API-ключ — он понадобится для подключения SDK к сайту
            </p>

            <label className={styles.label}>Ваш API-ключ</label>
            <div className={styles.apiKeyBox}>
              <span className={styles.apiKeyText}>{createdProject.apiKey}</span>
              <button
                className={styles.buttonSmall}
                onClick={async () => {
                  await copyToClipboard(createdProject.apiKey);
                  setCopied(true);
                  setTimeout(() => setCopied(false), 2000);
                }}
              >
                {copied ? "Скопировано ✓" : "Скопировать"}
              </button>
            </div>

            <label className={styles.label}>Установка</label>
            <div className={styles.codeBlock}>{`npm install @mcollector/sdk`}</div>

            <label className={styles.label}>Инициализация</label>
            <div className={styles.codeBlock}>{`import { analytics } from '@mcollector/sdk'\n\nanalytics.init('${createdProject.apiKey}')`}</div>

            <div className={styles.modalButtons}>
              <button
                className={styles.button}
                onClick={() => router.push(`/projects/${createdProject.id}/dashboard`)}
              >
                Перейти в дашборд
              </button>
              <button
                className={styles.buttonOutline}
                onClick={() => router.push("/projects")}
              >
                К проектам
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
