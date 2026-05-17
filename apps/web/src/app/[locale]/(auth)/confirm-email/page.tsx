"use client";

import { useEffect, useState, Suspense } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useSearchParams } from "next/navigation";
import { Link, useRouter } from "@/i18n/navigation";
import { BASE_URL } from "@/lib/constants";
import { copyToClipboard } from "@/lib/clipboard";
import styles from "../login.module.css";

function ConfirmEmailContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations("confirmEmail");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [error, setError] = useState("");
  const [createdProject, setCreatedProject] = useState<{ id: string; apiKey: string } | null>(null);
  const [copied, setCopied] = useState(false);
  const [resendLoading, setResendLoading] = useState(false);
  const [resendDone, setResendDone] = useState(false);
  const [resendEmail, setResendEmail] = useState("");
  const [emailConfirmed, setEmailConfirmed] = useState(false);

  useEffect(() => {
    document.title = t("pageTitle");

    const userId = searchParams.get("userId");
    const token = searchParams.get("token");

    async function confirm() {
      if (!userId || !token) {
        setError(t("invalidLink"));
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
          setError(text || t("confirmError"));
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

          setError(t("emailConfirmedProjectError"));
          setStatus("error");
          return;
        }

        setStatus("success");
        router.push("/projects");
      } catch {
        setError(t("connectionError"));
        setStatus("error");
      }
    }

    confirm();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  if (status === "loading") {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>{t("confirming")}</p>
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
        body: JSON.stringify({ email, locale }),
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
            <p className={styles.subtitle}>{t("emailConfirmed")}</p>
            <div className={styles.error}>{error}</div>
            <p className={styles.linkText}>
              <Link href="/projects" className={styles.link}>{t("goToProjects")}</Link>
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
          <p className={styles.subtitle}>{t("confirmationError")}</p>
          <div className={styles.error}>{error}</div>
          {resendDone ? (
            <p className={styles.linkText} style={{ color: "#16a34a" }}>{t("emailSent")}</p>
          ) : (
            <div style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 8 }}>
              {!knownEmail && (
                <input
                  type="email"
                  placeholder={t("yourEmail")}
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
                {resendLoading ? tCommon("sending") : t("resendEmail")}
              </button>
            </div>
          )}
          <p className={styles.linkText}>
            <Link href="/login" className={styles.link}>{t("logIn")}</Link>
          </p>
        </div>
      </div>
    );
  }

  if (!createdProject) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>{t("emailConfirmed")}</p>
          <p className={styles.linkText}>
            <Link href="/projects" className={styles.link}>{t("goToProjects")}</Link>
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h1 className={styles.title}>MCollector</h1>
        <p className={styles.subtitle}>{t("emailConfirmed")}</p>
      </div>

      <div className={styles.modalOverlay}>
        <div className={styles.modal}>
          <h2 className={styles.modalTitle}>{t("projectCreated")}</h2>
          <p className={styles.modalSubtitle}>{t("projectCreatedSubtitle")}</p>

          <label className={styles.label}>{t("yourApiKey")}</label>
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
              {copied ? tCommon("copied") : tCommon("copy")}
            </button>
          </div>

          <label className={styles.label}>{t("installation")}</label>
          <div className={styles.codeBlock}>{`npm install @mcollector/sdk`}</div>

          <label className={styles.label}>{t("initialization")}</label>
          <div className={styles.codeBlock}>{`import { analytics } from '@mcollector/sdk'\n\nanalytics.init('${createdProject.apiKey}')`}</div>

          <div className={styles.modalButtons}>
            <button
              className={styles.button}
              onClick={() => router.push(`/projects/${createdProject.id}/dashboard`)}
            >
              {t("goToDashboard")}
            </button>
            <button
              className={styles.buttonOutline}
              onClick={() => router.push("/projects")}
            >
              {t("toProjects")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function ConfirmEmailPage() {
  const tCommon = useTranslations("common");
  return (
    <Suspense fallback={
      <div className="min-h-screen flex items-center justify-center">
        <p>{tCommon("loading")}</p>
      </div>
    }>
      <ConfirmEmailContent />
    </Suspense>
  );
}
