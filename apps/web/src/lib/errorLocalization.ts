// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Translator = (key: string, values?: Record<string, any>) => string;

export function localizeErrorMessage(message: string, t: Translator): string {
  const text = message.trim();
  if (!text) return t("unknown");

  if (/^Request failed with status \d+$/i.test(text)) {
    const code = text.match(/\d+/)?.[0];
    return code ? t("requestFailed", { code }) : t("requestFailedNoCode");
  }

  if (/^Unauthorized$/i.test(text)) return t("unauthorized");
  if (/^Invalid or expired refresh token$/i.test(text)) return t("invalidRefreshToken");
  if (/^Passkey limit reached/i.test(text)) return t("passkeyLimitReached");
  if (/^Passkey is already registered/i.test(text)) return t("passkeyAlreadyRegistered");
  if (/^Passkey '.+' was not found$/i.test(text)) return t("passkeyNotFound");
  if (/^User '.+' is not authorized to perform this action$/i.test(text)) return t("notAuthorized");
  if (/^User with id '.+' was not found$/i.test(text)) return t("userNotFound");
  if (/^Registration failed$/i.test(text)) return t("registrationFailed");

  return text;
}
