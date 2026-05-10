"use client";

import { useEffect, useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { BASE_URL } from "@/lib/constants";
import { copyToClipboard } from "@/lib/clipboard";
import styles from "../login.module.css";

function ConfirmEmailContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [error, setError] = useState("");
  const [createdProject, setCreatedProject] = useState<{ id: string; apiKey: string } | null>(null);
  const [copied, setCopied] = useState(false);
  const [resendLoading, setResendLoading] = useState(false);
  const [resendDone, setResendDone] = useState(false);
  const [resendEmail, setResendEmail] = useState("");
  const [emailConfirmed, setEmailConfirmed] = useState(false);

  useEffect(() => {
    document.title = "MCollector — Подтверждение email";

    const userId = searchParams.get("userId");
    const token = searchParams.get("token");

    async function confirm() {
      if (!userId || !token) {
        setError("Неверная ссылка подтверждения");
        setStatus("error");
        return;
      }

      try {
        const res = await fetch(`${BASE_URL}/api/auth/confirm-email`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ userId, token }),
        });

        if (!res.ok) {
          const text = await res.text();
          setError(text || "Не удалось подтвердить email");
          setStatus("error");
          return;
        }

        const data = await res.json();
        localStorage.setItem("token", data.accessToken);
        localStorage.setItem("refreshToken", data.refreshToken);
        localStorage.removeItem("pendingEmail");
        setEmailConfirmed(true);

        const orgName = localStorage.getItem("pendingOrgName");
        if (orgName) {
          localStorage.removeItem("pendingOrgName");
          const projectRes = await fetch(`${BASE_URL}/api/projects`, {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              Authorization: `Bearer ${data.accessToken}`,
            },
            body: JSON.stringify({ name: orgName }),
          });

          if (projectRes.ok) {
            const project = await projectRes.json();
            setCreatedProject({ id: project.id, apiKey: project.apiKey });
            setStatus("success");
            return;
          }

          setError("Email подтверждён, но не удалось создать проект. Создайте его вручную в разделе проектов");
          setStatus("error");
          return;
        }

        setStatus("success");
        router.push("/projects");
      } catch {
        setError("Не удалось подключиться к серверу");
        setStatus("error");
      }
    }

    confirm();
  }, [searchParams, router]);

  if (status === "loading") {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>Подтверждение email...</p>
        </div>
      </div>
    );
  }

  async function handleResend() {
    const email = resendEmail || localStorage.getItem("pendingEmail") || "";
    if (!email) return;
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

  if (status === "error") {
    if (emailConfirmed) {
      return (
        <div className={styles.page}>
          <div className={styles.card}>
            <h1 className={styles.title}>MCollector</h1>
            <p className={styles.subtitle}>Email подтверждён!</p>
            <div className={styles.error}>{error}</div>
            <p className={styles.linkText}>
              <Link href="/projects" className={styles.link}>Перейти к проектам</Link>
            </p>
          </div>
        </div>
      );
    }

    const knownEmail = localStorage.getItem("pendingEmail");
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>Ошибка подтверждения</p>
          <div className={styles.error}>{error}</div>
          {resendDone ? (
            <p className={styles.linkText} style={{ color: "#16a34a" }}>Письмо отправлено</p>
          ) : (
            <div style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 8 }}>
              {!knownEmail && (
                <input
                  type="email"
                  placeholder="Ваш email"
                  value={resendEmail}
                  onChange={(e) => setResendEmail(e.target.value)}
                  className={styles.input}
                />
              )}
              <button
                onClick={handleResend}
                disabled={resendLoading || (!knownEmail && !resendEmail)}
                className={styles.button}
                style={{ marginTop: 0 }}
              >
                {resendLoading ? "Отправка..." : "Отправить письмо снова"}
              </button>
            </div>
          )}
          <p className={styles.linkText}>
            <Link href="/login" className={styles.link}>Войти</Link>
          </p>
        </div>
      </div>
    );
  }

  if (!createdProject) return null;

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>Email подтверждён!</p>
      </div>

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
    </div>
  );
}

export default function ConfirmEmailPage() {
  return (
    <Suspense fallback={
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>Загрузка...</p>
        </div>
      </div>
    }>
      <ConfirmEmailContent />
    </Suspense>
  );
}
