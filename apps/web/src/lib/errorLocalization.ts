export const UNAUTHORIZED_ERROR_MESSAGE = "Требуется повторный вход в систему.";

export function localizeErrorMessage(message: string): string {
  const text = message.trim();
  if (!text) return "Что-то пошло не так. Попробуйте ещё раз.";

  if (/^Request failed with status \d+$/i.test(text)) {
    const code = text.match(/\d+/)?.[0];
    return `Ошибка запроса${code ? ` (код ${code})` : ""}.`;
  }

  if (/^Unauthorized$/i.test(text)) return UNAUTHORIZED_ERROR_MESSAGE;
  if (/^Invalid or expired refresh token$/i.test(text)) return "Недействительный или истёкший токен обновления.";
  if (/^Passkey limit reached/i.test(text)) return "Достигнут лимит ключей доступа для этого аккаунта.";
  if (/^Passkey is already registered/i.test(text)) return "Ключ доступа уже добавлен для этого аккаунта.";
  if (/^Passkey '.+' was not found$/i.test(text)) return "Ключ доступа не найден.";
  if (/^User '.+' is not authorized to perform this action$/i.test(text)) {
    return "Недостаточно прав для выполнения действия.";
  }
  if (/^User with id '.+' was not found$/i.test(text)) return "Пользователь не найден.";
  if (/^Registration failed$/i.test(text)) return "Не удалось завершить регистрацию ключа доступа.";

  return text;
}
