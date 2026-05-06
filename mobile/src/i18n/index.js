import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import * as Localization from 'expo-localization';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { I18nManager } from 'react-native';

import en from '../locales/en.json';
import fr from '../locales/fr.json';
import tn from '../locales/tn.json';

export const STORAGE_LANGUAGE_KEY = '@agrismart_language';

export const LANGUAGE_CODES = ['en', 'fr', 'tn'];

/** Tunisian UI uses Arabic script → enable RTL mirroring for layout. */
export function applyLayoutDirection(lng) {
  const rtl = lng === 'tn';
  try {
    if (I18nManager.isRTL !== rtl) {
      I18nManager.allowRTL(rtl);
      I18nManager.forceRTL(rtl);
    }
  } catch (_) {
    /* ignore */
  }
}

/**
 * Call once at app startup (sync init + async preferred language).
 */
export function initI18n() {
  i18n.use(initReactI18next).init({
    resources: {
      en: { translation: en },
      fr: { translation: fr },
      tn: { translation: tn },
    },
    lng: 'en',
    fallbackLng: 'en',
    compatibilityJSON: 'v3',
    interpolation: { escapeValue: false },
  });

  applyLayoutDirection(i18n.language);

  (async () => {
    try {
      const saved = await AsyncStorage.getItem(STORAGE_LANGUAGE_KEY);
      if (saved && LANGUAGE_CODES.includes(saved)) {
        await i18n.changeLanguage(saved);
        applyLayoutDirection(saved);
        return;
      }
      const code = Localization.getLocales?.()?.[0]?.languageCode?.toLowerCase();
      if (code === 'fr') {
        await i18n.changeLanguage('fr');
        applyLayoutDirection('fr');
      } else if (code === 'ar' || code?.startsWith('ar')) {
        await i18n.changeLanguage('tn');
        applyLayoutDirection('tn');
      }
    } catch (_) {
      /* keep default */
    }
  })();
}

export async function setAppLanguage(lng) {
  if (!LANGUAGE_CODES.includes(lng)) return;
  await AsyncStorage.setItem(STORAGE_LANGUAGE_KEY, lng);
  await i18n.changeLanguage(lng);
  applyLayoutDirection(lng);
}

export default i18n;
