"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { BASE_URL } from "@/lib/constants";
import styles from "../login.module.css";

function ResetPasswordContent() {
  const searchParams = useSearchParams();
  const t = useTranslations("resetPassword");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  const userId = searchParams.get("userId");
  const token = searchParams.get("token");

  const passwordRequirements = [
    { check: password.length >= 8, label: t("req_minLength") },
    { check: /[a-z]/.test(password), label: t("req_lowercase") },
    { check: /[A-Z]/.test(password), label: t("req_uppercase") },
    { check: /[^a-zA-Z0-9]/.test(password), label: t("req_special") },
  ];

  useEffect(() => {
    document.title = t("pageTitle");
  }, [t]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!userId || !token) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${BASE_URL}/api/auth/reset-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userId, token, password }),
      });

      if (!res.ok) {
        const text = await res.text();
        setError(text || t("resetError"));
        return;
      }

      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      setDone(true);
    } catch {
      setError(t("connectionError"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        {!userId || !token ? (
          <>
            <p className={styles.subtitle}>{t("invalidLink")}</p>
            <p className={styles.linkText}>
              <Link href="/forgot-password" className={styles.link}>{t("requestNew")}</Link>
            </p>
          </>
        ) : done ? (
          <>
            <p className={styles.subtitle}>{t("passwordChanged")}</p>
            <p style={{ fontSize: 14, color: "#3f3f46", textAlign: "center", lineHeight: 1.6 }}>
              {t("logInWithNew")}
            </p>
            <p className={styles.linkText}>
              <Link href="/login" className={styles.link}>{t("logIn")}</Link>
            </p>
          </>
        ) : (
          <>
            <p className={styles.subtitle}>{t("subtitle")}</p>
            <form onSubmit={handleSubmit} className={styles.form}>
              {error && <div className={styles.error}>{error}</div>}

              <div>
                <label className={styles.label}>{t("newPasswordLabel")}</label>
                <div className={styles.passwordWrapper}>
                  <input
                    type={showPassword ? "text" : "password"}
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => { setPassword(e.target.value); setError(""); }}
                    required
                    minLength={8}
                    autoFocus
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

              <button type="submit" disabled={loading} className={styles.button}>
                {loading ? t("saving") : t("savePassword")}
              </button>
              <p className={styles.linkText}>
                <Link href="/login" className={styles.link}>{t("logIn")}</Link>
              </p>
            </form>
          </>
        )}
      </div>
    </div>
  );
}

export default function ResetPasswordPage() {
  const tCommon = useTranslations("common");
  return (
    <Suspense fallback={
      <div className="min-h-screen flex items-center justify-center">
        <p>{tCommon("loading")}</p>
      </div>
    }>
      <ResetPasswordContent />
    </Suspense>
  );
}
