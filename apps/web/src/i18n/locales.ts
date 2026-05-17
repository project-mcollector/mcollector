export const localeLabels = {
  en: "English",
  ru: "Русский",
  de: "Deutsch",
  fr: "Français",
  es: "Español",
  zh: "中文",
} as const;

export type AppLocale = keyof typeof localeLabels;
export const locales = Object.keys(localeLabels) as [AppLocale, ...AppLocale[]];
