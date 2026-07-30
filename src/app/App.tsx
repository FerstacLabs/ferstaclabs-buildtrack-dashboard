import { ConfigProvider } from 'antd'
import { RouterProvider } from 'react-router-dom'
import { I18nProvider } from '../i18n'
import { router } from './routes'
import { theme } from './theme'

const App = () => (
  <I18nProvider>
    <ConfigProvider theme={theme}>
      <RouterProvider router={router} />
    </ConfigProvider>
  </I18nProvider>
)

export default App
