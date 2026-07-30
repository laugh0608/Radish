import { theme as antdThemeEngine, type ThemeConfig } from 'antd';

/**
 * Radish family-ui Workbench 亮色语义
 */
export const radishColors = {
  brand: '#b24057',
  primary: '#435c74',
  primaryHover: '#55738f',
  primarySoft: 'rgba(67, 92, 116, 0.12)',
  success: '#4f9c83',
  warning: '#b5826d',
  error: '#c3564d',
  info: '#435c74',
  background: '#f7f4ee',
  surface: '#fffdf8',
  muted: '#f3eee5',
  text: '#2f2a25',
  textSecondary: '#6a5c4f',
  border: 'rgba(136, 99, 73, 0.16)',
};

/**
 * Ant Design 默认使用 family-ui Workbench Profile。
 *
 * 文档：https://ant.design/docs/react/customize-theme-cn
 */
export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: radishColors.primary,
    colorSuccess: radishColors.success,
    colorWarning: radishColors.warning,
    colorError: radishColors.error,
    colorInfo: radishColors.info,
    colorBgBase: radishColors.background,
    colorBgContainer: radishColors.surface,
    colorTextBase: radishColors.text,
    colorTextSecondary: radishColors.textSecondary,
    colorBorder: radishColors.border,
    colorLink: radishColors.primary,
    colorLinkHover: radishColors.primaryHover,
    fontSize: 14,
    fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, "PingFang SC", "Microsoft YaHei UI", "Noto Sans SC", sans-serif',
    borderRadius: 8,
    controlHeight: 32,
    boxShadow: '0 8px 24px rgba(91, 66, 44, 0.08)',
  },
  components: {
    Button: {
      controlHeight: 32,
      borderRadius: 8,
      fontWeight: 500,
    },
    Table: {
      headerBg: radishColors.muted,
      headerColor: radishColors.text,
      borderColor: radishColors.border,
    },
    Menu: {
      itemBg: 'transparent',
      itemSelectedBg: radishColors.primarySoft,
      itemSelectedColor: radishColors.primary,
      itemHoverBg: 'rgba(67, 92, 116, 0.08)',
    },
    Form: {
      labelColor: radishColors.text,
      labelFontSize: 14,
    },
    Modal: {
      headerBg: radishColors.surface,
      contentBg: radishColors.surface,
      borderRadiusLG: 12,
    },
  },
};

/**
 * family-ui 暗色语义。宿主可继续覆盖具体产品主题取值。
 */
export const antdDarkTheme: ThemeConfig = {
  ...antdTheme,
  algorithm: antdThemeEngine.darkAlgorithm,
  token: {
    ...antdTheme.token,
    colorPrimary: '#7fa0bd',
    colorSuccess: '#6fb39c',
    colorWarning: '#c9997f',
    colorError: '#d4726a',
    colorInfo: '#7fa0bd',
    colorBgBase: '#1a1713',
    colorBgContainer: '#23201a',
    colorTextBase: '#ede5d8',
    colorTextSecondary: '#b8ab9a',
    colorBorder: 'rgba(237, 229, 216, 0.14)',
    colorLink: '#7fa0bd',
    colorLinkHover: '#98b4cd',
    boxShadow: '0 8px 24px rgba(0, 0, 0, 0.36)',
  },
  components: {
    ...antdTheme.components,
    Table: {
      headerBg: '#2b2721',
      headerColor: '#ede5d8',
      borderColor: 'rgba(237, 229, 216, 0.14)',
    },
    Menu: {
      itemBg: 'transparent',
      itemSelectedBg: 'rgba(127, 160, 189, 0.18)',
      itemSelectedColor: '#7fa0bd',
      itemHoverBg: 'rgba(127, 160, 189, 0.12)',
    },
    Form: {
      labelColor: '#ede5d8',
      labelFontSize: 14,
    },
    Modal: {
      headerBg: '#23201a',
      contentBg: '#23201a',
      borderRadiusLG: 12,
    },
  },
};
