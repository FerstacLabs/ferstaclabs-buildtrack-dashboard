import type { ThemeConfig } from 'antd'

export const theme: ThemeConfig = {
  token: {
    colorPrimary: '#1479ff',
    colorSuccess: '#078b55',
    colorWarning: '#ff8a00',
    colorError: '#ef233c',
    colorText: '#071b55',
    colorTextSecondary: '#50607f',
    colorBorder: '#e4eaf3',
    borderRadius: 8,
    fontFamily: "Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
  },
  components: {
    Button: {
      controlHeight: 42,
      borderRadius: 8,
      fontWeight: 700,
    },
    Card: {
      borderRadiusLG: 10,
    },
    Table: {
      borderColor: '#e6edf6',
      headerBg: '#fbfdff',
      headerColor: '#06195a',
      rowHoverBg: '#f7fbff',
    },
    Select: {
      controlHeight: 44,
    },
    DatePicker: {
      controlHeight: 44,
    },
  },
}
