import { ConfigProvider } from 'antd'
import azAZ from 'antd/locale/az_AZ'
import enUS from 'antd/locale/en_US'
import ruRU from 'antd/locale/ru_RU'
import { RouterProvider } from 'react-router-dom'
import { I18nProvider, useI18n } from '../i18n'
import { router } from './routes'
import { theme } from './theme'

const localeByLanguage = {
  az: azAZ,
  en: enUS,
  ru: ruRU,
}

const AppShell = () => {
  const { language } = useI18n()
  return (
    <ConfigProvider theme={theme} locale={localeByLanguage[language]}>
      <RouterProvider router={router} />
    </ConfigProvider>
  )
}

const App = () => (
  <I18nProvider>
    <AppShell />
  </I18nProvider>
)

export default App
