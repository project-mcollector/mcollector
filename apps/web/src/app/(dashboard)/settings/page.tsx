"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { authFetch } from "@/lib/auth";
import { localizeErrorMessage } from "@/lib/errorLocalization";
import { getPasskeyLabel } from "@/lib/passkeyLabels";
import { Button } from "@/components/ui/button";

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

interface PasskeyCreationOptionsPayload {
  challenge: string;
  user: {
    id: string;
    name: string;
    displayName: string;
    [key: string]: unknown;
  };
  excludeCredentials?: Array<{
    type: string;
    id: string;
  }>;
  [key: string]: unknown;
}

interface PasskeyInfo {
  credentialId: string;
  createdAt: string;
  transports: string[];
  isBackupEligible: boolean;
  aaguid: string | null;
}

const MAX_PASSKEYS = 5;

export default function SettingsPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [loadingPasskeys, setLoadingPasskeys] = useState(true);
  const [passkeys, setPasskeys] = useState<PasskeyInfo[]>([]);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");

  async function getPasskeys(): Promise<PasskeyInfo[]> {
    const res = await authFetch("/api/auth/passkey", router);
    return (await res.json()) as PasskeyInfo[];
  }

  useEffect(() => {
    let cancelled = false;

    const init = async () => {
      try {
        const data = await getPasskeys();
        if (!cancelled) setPasskeys(data);
      } catch (err) {
        if (!cancelled) {
          setStatus("error");
          setErrorMessage(
            err instanceof Error
              ? localizeErrorMessage(err.message)
              : localizeErrorMessage(""),
          );
        }
      } finally {
        if (!cancelled) setLoadingPasskeys(false);
      }
    };

    void init();
    return () => {
      cancelled = true;
    };
  }, [router]);

  useEffect(() => {
    if (status !== "success") return;
    const id = setTimeout(() => setStatus("idle"), 3000);
    return () => clearTimeout(id);
  }, [status]);

  async function handleDeletePasskey(credentialId: string) {
    setDeletingId(credentialId);
    setStatus("idle");
    setErrorMessage("");
    try {
      await authFetch(`/api/auth/passkey/${encodeURIComponent(credentialId)}`, router, {
        method: "DELETE",
      });
      setPasskeys((prev) => prev.filter((p) => p.credentialId !== credentialId));
    } catch (err) {
      setStatus("error");
      setErrorMessage(
        err instanceof Error ? localizeErrorMessage(err.message) : localizeErrorMessage(""),
      );
    } finally {
      setDeletingId(null);
    }
  }

  async function handleCreatePasskey() {
    if (passkeys.length >= MAX_PASSKEYS) {
      setStatus("error");
      setErrorMessage(`Достигнут лимит passkey (${MAX_PASSKEYS}).`);
      return;
    }

    setLoading(true);
    setStatus("idle");
    setErrorMessage("");

    try {
      const optionsRes = await authFetch("/api/auth/passkey/register/options", router, {
        method: "POST",
      });
      const optionsPayload = (await optionsRes.json()) as unknown;
      const raw =
        typeof optionsPayload === "string"
          ? (JSON.parse(optionsPayload) as PasskeyCreationOptionsPayload)
          : (optionsPayload as PasskeyCreationOptionsPayload);

      const publicKeyOptions = {
        ...raw,
        challenge: base64urlToBuffer(raw.challenge),
        user: {
          ...raw.user,
          id: base64urlToBuffer(raw.user.id),
        },
        excludeCredentials: raw.excludeCredentials?.map(
          (c: { type: string; id: string }) => ({
            type: c.type as PublicKeyCredentialType,
            id: base64urlToBuffer(c.id),
          })
        ),
      } as PublicKeyCredentialCreationOptions;

      const credential = (await navigator.credentials.create({
        publicKey: publicKeyOptions,
      })) as PublicKeyCredential | null;

      if (!credential) {
        setStatus("error");
        setErrorMessage("Устройство не вернуло учётные данные.");
        return;
      }

      const attestation = credential.response as AuthenticatorAttestationResponse;
      const credentialJson = JSON.stringify({
        id: credential.id,
        rawId: bufferToBase64url(credential.rawId),
        type: credential.type,
        response: {
          attestationObject: bufferToBase64url(attestation.attestationObject),
          clientDataJSON: bufferToBase64url(attestation.clientDataJSON),
        },
        clientExtensionResults: {},
      });

      await authFetch("/api/auth/passkey/register", router, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ credentialJson }),
      });

      try {
        setPasskeys(await getPasskeys());
      } catch {
        // list refresh failed; passkey was still registered
      }
      setStatus("success");
    } catch (err) {
      if (err instanceof DOMException && err.name === "NotAllowedError") {
        // user cancelled — stay idle
      } else {
        setStatus("error");
        if (err instanceof DOMException && err.name === "InvalidStateError") {
          setErrorMessage("Passkey для этого аккаунта уже существует на текущем устройстве.");
        } else if (err instanceof DOMException && err.name === "SecurityError") {
          setErrorMessage(
            "Ошибка безопасности браузера. Убедитесь, что сайт открыт по HTTPS и Passkey__ServerDomain совпадает с доменом сайта."
          );
        } else {
          setErrorMessage(
            err instanceof Error ? localizeErrorMessage(err.message) : localizeErrorMessage(""),
          );
        }
      }
    } finally {
      setLoading(false);
    }
  }

  const passkeySupported = typeof window !== "undefined" && "credentials" in navigator;

  return (
    <div className="p-8 max-w-2xl">
      <h1 className="text-2xl font-semibold mb-8">Настройки</h1>

      <section>
        <h2 className="mb-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Безопасность
        </h2>
        <div className="rounded-lg bg-sidebar p-6">
          <h3 className="font-medium mb-1">Passkey</h3>
          <p className="text-sm text-muted-foreground mb-4">
            Входите без пароля с помощью биометрии или PIN-кода вашего устройства.
          </p>

          {loadingPasskeys ? (
            <p className="text-sm text-muted-foreground mb-4">Загрузка passkey…</p>
          ) : passkeys.length > 0 ? (
            <ul className="mb-4 divide-y divide-border -mx-6">
              {passkeys.map((passkey) => (
                <li key={passkey.credentialId} className="flex items-center justify-between gap-2 px-6 py-2.5 text-sm">
                  <div>
                    <span className="font-medium">{getPasskeyLabel(passkey.aaguid, passkey.transports, passkey.isBackupEligible)}</span>
                    <span className="text-muted-foreground"> — создан {new Date(passkey.createdAt).toLocaleString("ru-RU")}</span>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => void handleDeletePasskey(passkey.credentialId)}
                    disabled={deletingId === passkey.credentialId}
                    className="shrink-0 text-destructive hover:text-destructive"
                  >
                    {deletingId === passkey.credentialId ? "…" : "Удалить"}
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-muted-foreground mb-4">Passkey пока не добавлен.</p>
          )}

          {status === "success" && (
            <p className="text-sm text-green-600 mb-3">Passkey успешно добавлен.</p>
          )}
          {status === "error" && (
            <p className="text-sm text-destructive mb-3">{errorMessage}</p>
          )}

          <div className="flex items-center justify-between gap-4">
            <Button
              variant="outline"
              onClick={handleCreatePasskey}
              disabled={loading || loadingPasskeys || !passkeySupported || passkeys.length >= MAX_PASSKEYS}
            >
              {loading
                ? "Ожидание устройства…"
                : passkeys.length >= MAX_PASSKEYS
                  ? "Достигнут лимит passkey"
                  : "Добавить passkey"}
            </Button>
            {!loadingPasskeys && (
              <span className="text-xs text-muted-foreground">{passkeys.length} / {MAX_PASSKEYS}</span>
            )}
          </div>

          {!passkeySupported && (
            <p className="mt-2 text-xs text-muted-foreground">
              Ваш браузер не поддерживает passkey.
            </p>
          )}
        </div>
      </section>
    </div>
  );
}
