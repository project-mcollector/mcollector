"use client";

import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { Link } from "@/i18n/navigation";
import { BASE_URL } from "@/lib/constants";
import styles from "../login.module.css";

export default function ForgotPasswordPage() {
  const t = useTranslations("forgotPassword");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    document.title = t("pageTitle");
  }, [t]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await fetch(`${BASE_URL}/api/auth/forgot-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, locale }),
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
        <p className={styles.subtitle}>{t("subtitle")}</p>

        {done ? (
          <>
            <p style={{ fontSize: 14, color: "#3f3f46", textAlign: "center", lineHeight: 1.6 }}>
              {t("sentInfo", { email })}
            </p>
            <p className={styles.linkText}>
              <Link href="/login" className={styles.link}>{t("logIn")}</Link>
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
              {loading ? tCommon("sending") : t("sendLink")}
            </button>
            <p className={styles.linkText}>
              <Link href="/login" className={styles.link}>{t("backToLogin")}</Link>
            </p>
          </form>
        )}
      </div>
    </div>
  );
}
