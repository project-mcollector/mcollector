"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { BASE_URL } from "@/lib/constants";
import { isTokenFresh } from "@/lib/auth";
import styles from "../login.module.css";
import IntegrationCard from "../IntegrationCard";

interface PasskeyRequestOptions {
  challenge: string;
  rpId?: string;
  timeout?: number;
  allowCredentials?: { type: string; id: string }[];
  userVerification?: string;
}

async function readErrorMessage(res: Response, fallback: string): Promise<string> {
  const contentType = res.headers.get("content-type") || "";
  if (contentType.includes("application/json")) {
    try {
      const body = (await res.json()) as unknown;
      if (typeof body === "string" && body.trim()) return body;
      if (body && typeof body === "object") {
        const obj = body as { message?: unknown; error?: unknown; description?: unknown };
        if (typeof obj.message === "string" && obj.message.trim()) return obj.message;
        if (typeof obj.error === "string" && obj.error.trim()) return obj.error;
        if (typeof obj.description === "string" && obj.description.trim()) return obj.description;
      }
    } catch {
      // noop
    }
  }

  try {
    const text = (await res.text()).trim();
    return text || fallback;
  } catch {
    return fallback;
  }
}

function base64urlToBuffer(base64url: string): ArrayBuffer {
  const base64 = base64url.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

function bufferToBase64url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=/g, "");
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
  const [passkeyLoading, setPasskeyLoading] = useState(false);
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

  if (!ready) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>MCollector</h1>
          <p className={styles.subtitle}>Загрузка...</p>
        </div>
      </div>
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    setEmailUnconfirmed(false);
    setResendDone(false);

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

  async function handlePasskeyLogin() {
    setPasskeyLoading(true);
    setError("");
    setEmailUnconfirmed(false);

    try {
      const optionsRes = await fetch("/api/auth/passkey/options", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ email: email || undefined }),
      });

      if (!optionsRes.ok) {
        setError(await readErrorMessage(optionsRes, "Не удалось получить параметры Passkey"));
        return;
      }

      const optionsPayload = (await optionsRes.json()) as unknown;
      const raw =
        typeof optionsPayload === "string"
          ? (JSON.parse(optionsPayload) as PasskeyRequestOptions)
          : (optionsPayload as PasskeyRequestOptions);

      const publicKeyOptions: PublicKeyCredentialRequestOptions = {
        ...raw,
        challenge: base64urlToBuffer(raw.challenge),
        allowCredentials: raw.allowCredentials?.map(c => ({
          type: c.type as PublicKeyCredentialType,
          id: base64urlToBuffer(c.id),
        })),
        userVerification: raw.userVerification as UserVerificationRequirement | undefined,
      };

      const credential = await navigator.credentials.get({ publicKey: publicKeyOptions }) as PublicKeyCredential | null;
      if (!credential) return;

      const assertion = credential.response as AuthenticatorAssertionResponse;
      const credentialJson = JSON.stringify({
        id: credential.id,
        rawId: bufferToBase64url(credential.rawId),
        type: credential.type,
        response: {
          authenticatorData: bufferToBase64url(assertion.authenticatorData),
          clientDataJSON: bufferToBase64url(assertion.clientDataJSON),
          signature: bufferToBase64url(assertion.signature),
          userHandle: assertion.userHandle ? bufferToBase64url(assertion.userHandle) : null,
        },
        clientExtensionResults: {},
      });

      const loginRes = await fetch("/api/auth/passkey/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ credentialJson }),
      });

      if (!loginRes.ok) {
        setError(await readErrorMessage(loginRes, "Passkey не прошёл проверку"));
        return;
      }

      const data = await loginRes.json();
      localStorage.setItem("token", data.accessToken);
      localStorage.setItem("refreshToken", data.refreshToken);
      router.push("/projects");
    } catch (err) {
      if (err instanceof DOMException && err.name === "NotAllowedError") return;
      if (err instanceof DOMException && err.name === "SecurityError") {
        setError(
          "Ошибка безопасности браузера. Проверьте HTTPS и что домен сайта совпадает с PASSKEY_SERVER_DOMAIN."
        );
        return;
      }
      if (err instanceof Error && err.message.trim()) {
        setError(err.message);
        return;
      }
      setError("Не удалось войти с Passkey");
    } finally {
      setPasskeyLoading(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.twoCol}>
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
            <div className={styles.labelRow}>
              <label className={styles.label}>Пароль</label>
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
          <div className={styles.divider}>или</div>
          <button
            type="button"
            onClick={handlePasskeyLogin}
            disabled={passkeyLoading || loading || !navigator.credentials}
            className={styles.buttonPasskey}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="7.5" cy="15.5" r="5.5"/><path d="m21 2-9.6 9.6"/><path d="m15.5 7.5 3 3L22 7l-3-3"/></svg>
            {passkeyLoading ? "Ожидание..." : "Войти с Passkey"}
          </button>
          <p className={styles.linkText}>
            Нет аккаунта?{" "}
            <Link href="/register" className={styles.link}>
              Зарегистрироваться
            </Link>
          </p>
        </form>
      </div>
      <IntegrationCard />
      </div>
    </div>
  );
}
