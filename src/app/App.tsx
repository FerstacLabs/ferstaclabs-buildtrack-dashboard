import { ConfigProvider } from 'antd'
import { RouterProvider } from 'react-router-dom'
import { router } from './routes'
import { theme } from './theme'

const App = () => (
  <ConfigProvider theme={theme}>
    <RouterProvider router={router} />
  </ConfigProvider>
)

export default App
