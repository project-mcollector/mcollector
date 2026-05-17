"use client";

import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useRouter, Link } from "@/i18n/navigation";
import styles from "../login.module.css";
import { BASE_URL } from "@/lib/constants";
import { isTokenFresh } from "@/lib/auth";
import IntegrationCard from "../IntegrationCard";
import { LocaleSwitcher } from "@/components/locale-switcher";

export default function RegisterPage() {
  const router = useRouter();
  const t = useTranslations("register");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [organizationName, setOrganizationName] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);
  const [success, setSuccess] = useState(false);
  const [resendLoading, setResendLoading] = useState(false);
  const [resendDone, setResendDone] = useState(false);

  useEffect(() => {
    document.title = t("pageTitle");
    const token = localStorage.getItem("token");
    if (token && isTokenFresh(token)) {
      router.replace("/projects");
    } else {
      setReady(true);
    }
  }, [router, t]);

  if (!ready) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>{tCommon("loading")}</p>
        </div>
      </div>
    );
  }

  const passwordRequirements = [
    { check: password.length >= 8, label: t("req_minLength") },
    { check: /[a-z]/.test(password), label: t("req_lowercase") },
    { check: /[A-Z]/.test(password), label: t("req_uppercase") },
    { check: /[^a-zA-Z0-9]/.test(password), label: t("req_special") },
  ];

  const passwordsMatch = confirmPassword.length > 0 && password === confirmPassword;
  const passwordsMismatch = confirmPassword.length > 0 && password !== confirmPassword;

  function validatePassword(pwd: string): boolean {
    return (
      pwd.length >= 8 &&
      /[a-z]/.test(pwd) &&
      /[A-Z]/.test(pwd) &&
      /[^a-zA-Z0-9]/.test(pwd)
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError(t("passwordsMismatch"));
      return;
    }

    if (!validatePassword(password)) {
      setError(t("passwordTooWeak"));
      return;
    }

    setLoading(true);
    localStorage.setItem("pendingOrgName", organizationName);
    localStorage.setItem("pendingEmail", email);

    try {
      const res = await fetch(`${BASE_URL}/api/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, organizationName, locale }),
      });

      if (!res.ok) {
        localStorage.removeItem("pendingOrgName");
        localStorage.removeItem("pendingEmail");
        const text = await res.text();
        setError(text || t("registrationError"));
        return;
      }

      setSuccess(true);
    } catch {
      localStorage.removeItem("pendingOrgName");
      localStorage.removeItem("pendingEmail");
      setError(t("connectionError"));
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
        body: JSON.stringify({ email, locale }),
      });
      setResendDone(true);
    } finally {
      setResendLoading(false);
    }
  }

  if (success) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>{t("checkEmail")}</p>
          <p style={{ fontSize: 14, color: "#3f3f46", textAlign: "center", lineHeight: 1.6 }}>
            {t.rich("emailSentTo", { email, b: (chunks) => <strong key="b">{chunks}</strong> })}
            <br />
            {t("clickLink")}
          </p>
          <p className={styles.linkText}>
            {t("noEmail")}{" "}
            {resendDone ? (
              <span style={{ color: "#16a34a", fontWeight: 500 }}>{t("emailSent")}</span>
            ) : (
              <button
                onClick={handleResend}
                disabled={resendLoading}
                style={{ background: "none", border: "none", cursor: "pointer", padding: 0 }}
                className={styles.link}
              >
                {resendLoading ? tCommon("sending") : tCommon("sending").replace("...", "") + "…"}
              </button>
            )}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <div className={styles.twoCol}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>{t("subtitle")}</p>

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
            <label className={styles.label}>{t("organizationName")}</label>
            <input
              type="text"
              placeholder={t("organizationPlaceholder")}
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              required
              autoComplete="organization"
              disabled={loading}
              className={styles.input}
            />
          </div>

          <div>
            <label className={styles.label}>{t("password")}</label>
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
            <label className={styles.label}>{t("confirmPassword")}</label>
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
                {passwordsMatch ? t("passwordsMatch") : passwordsMismatch ? t("passwordsDoNotMatch") : ""}
              </div>
            )}
          </div>

          <button type="submit" disabled={loading} className={styles.button}>
            {loading ? t("registering") : t("createAccount")}
          </button>
        </form>

        <p className={styles.linkText}>
          {t("alreadyHaveAccount")}{" "}
          <Link href="/login" className={styles.link}>
            {t("logIn")}
          </Link>
        </p>
        <div style={{ display: "flex", justifyContent: "center", marginTop: 8 }}>
          <LocaleSwitcher />
        </div>
      </div>
      <IntegrationCard />
      </div>
    </div>
  );
}
