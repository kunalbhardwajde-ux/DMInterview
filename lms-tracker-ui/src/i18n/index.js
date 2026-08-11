import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from '../locales/en.json'

// Resources are bundled (not fetched via an HTTP backend), so init resolves
// synchronously and every useTranslation() call works on first render - no
// loading state needed, and no <I18nextProvider> wrapper required since this
// initializes the default i18next singleton react-i18next reads from.
i18next.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  resources: {
    en: { translation: en },
  },
  interpolation: {
    escapeValue: false,
  },
})

export default i18next
