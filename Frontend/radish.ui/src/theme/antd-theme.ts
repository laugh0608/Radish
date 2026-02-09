import type { ThemeConfig } from 'antd';

/**
 * Radish 主题色配置
 */
export const radishColors = {
  /** 主色调 - 萝卜橙 */
  primary: '#FF6B35',
  /** 成功色 */
  success: '#52c41a',
  /** 警告色 */
  warning: '#faad14',
  /** 错误色 */
  error: '#ff4d4f',
  /** 信息色 */
  info: '#1890ff',
  /** 背景色 */
  background: '#f5f5f5',
  /** 文字色 */
  text: '#333333',
  /** 次要文字色 */
  textSecondary: '#666666',
  /** 边框色 */
  border: '#d9d9d9',
};

/**
 * Ant Design 主题配置
 *
 * 文档：https://ant.design/docs/react/customize-theme-cn
 */
export const antdTheme: ThemeConfig = {
  token: {
    // 主色调
    colorPrimary: radishColors.primary,
    colorSuccess: radishColors.success,
    colorWarning: radishColors.warning,
    colorError: radishColors.error,
    colorInfo: radishColors.info,

    // 字体
    fontSize: 14,
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',

    // 圆角
    borderRadius: 6,

    // 间距
    controlHeight: 32,

    // 阴影
    boxShadow: '0 2px 8px rgba(0, 0, 0, 0.08)',
  },
  components: {
    // Button 组件定制
    Button: {
      controlHeight: 32,
      borderRadius: 6,
      fontWeight: 500,
    },
    // Table 组件定制
    Table: {
      headerBg: '#fafafa',
      headerColor: '#333333',
      borderColor: '#e8e8e8',
    },
    // Menu 组件定制
    Menu: {
      itemBg: 'transparent',
      itemSelectedBg: 'rgba(255, 107, 53, 0.1)',
      itemSelectedColor: radishColors.primary,
      itemHoverBg: 'rgba(255, 107, 53, 0.05)',
    },
    // Form 组件定制
    Form: {
      labelColor: '#333333',
      labelFontSize: 14,
    },
    // Modal 组件定制
    Modal: {
      headerBg: '#ffffff',
      contentBg: '#ffffff',
      borderRadiusLG: 8,
    },
  },
};

/**
 * 暗色主题配置
 */
export const antdDarkTheme: ThemeConfig = {
  ...antdTheme,
  token: {
    ...antdTheme.token,
    colorBgBase: '#141414',
    colorTextBase: '#ffffff',
  },
};
