"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { authFetch } from "@/lib/auth";
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

export default function SettingsPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<"idle" | "success" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");

  async function handleCreatePasskey() {
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

      setStatus("success");
    } catch (err) {
      if (err instanceof DOMException && err.name === "NotAllowedError") {
        // user cancelled — stay idle
      } else {
        setStatus("error");
        if (err instanceof DOMException && err.name === "SecurityError") {
          setErrorMessage(
            "Ошибка безопасности браузера. Убедитесь, что сайт открыт по HTTPS и Passkey__ServerDomain совпадает с доменом сайта."
          );
        } else if (err instanceof Error && err.message.trim()) {
          setErrorMessage(err.message);
        } else {
          setErrorMessage("Что-то пошло не так. Попробуйте ещё раз.");
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
        <div className="rounded-lg border p-6">
          <h3 className="font-medium mb-1">Passkey</h3>
          <p className="text-sm text-muted-foreground mb-4">
            Входите без пароля с помощью биометрии или PIN-кода вашего устройства.
          </p>

          {status === "success" && (
            <p className="text-sm text-green-600 mb-3">Passkey успешно добавлен.</p>
          )}
          {status === "error" && (
            <p className="text-sm text-destructive mb-3">{errorMessage}</p>
          )}

          <Button
            variant="outline"
            onClick={handleCreatePasskey}
            disabled={loading || !passkeySupported}
          >
            {loading ? "Ожидание устройства…" : "Добавить passkey"}
          </Button>

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
