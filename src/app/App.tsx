import { ConfigProvider } from 'antd'
import azAZ from 'antd/locale/az_AZ'
import enUS from 'antd/locale/en_US'
import ruRU from 'antd/locale/ru_RU'
import { RouterProvider } from 'react-router-dom'
import { I18nProvider, useI18n } from '../i18n'
import { MarketingLandingPage } from '../features/marketing/MarketingLandingPage'
import { getHostMode } from './hostMode'
import { fieldRouter } from './fieldRoutes'
import { router } from './routes'
import { supplyRouter } from './supplyRoutes'
import { theme } from './theme'

const localeByLanguage = {
  az: azAZ,
  en: enUS,
  ru: ruRU,
}

const AppShell = () => {
  const { language } = useI18n()
  const hostMode = getHostMode()

  return (
    <ConfigProvider theme={theme} locale={localeByLanguage[language]}>
      {hostMode === 'Marketing'
        ? <MarketingLandingPage />
        : <RouterProvider router={hostMode === 'FieldPortal' ? fieldRouter : hostMode === 'SupplyPortal' ? supplyRouter : router} />}
    </ConfigProvider>
  )
}

const App = () => (
  <I18nProvider>
    <AppShell />
  </I18nProvider>
)

export default App
