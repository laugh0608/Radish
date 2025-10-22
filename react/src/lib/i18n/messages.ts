// 本地化文案表：仅含简体中文与英语两种语言
export type Locale = 'en' | 'zh-CN'

// 重要说明：键名采用命名空间前缀，便于分组管理
export const messages: Record<Locale, Record<string, string>> = {
  en: {
    'nav.docs': 'Docs',
    'nav.features': 'Features',
    'nav.community': 'Community',
    'actions.signUp': 'Sign up',
    'actions.signIn': 'Sign in',
    'aria.toggleNav': 'Toggle navigation menu',
    'aria.langSwitcher': 'Switch language',
    'lang.zhCN': '中文',
    'lang.en': 'English',
    'app.welcome': 'Welcome to Radish',
    'app.getStarted': 'Start building your React app.',
  },
  'zh-CN': {
    'nav.docs': '文档',
    'nav.features': '特性',
    'nav.community': '社区',
    'actions.signUp': '注册',
    'actions.signIn': '登录',
    'aria.toggleNav': '切换导航菜单',
    'aria.langSwitcher': '切换语言',
    'lang.zhCN': '中文',
    'lang.en': '英文',
    'app.welcome': '欢迎来到 Radish',
    'app.getStarted': '开始构建你的 React 应用。',
  },
}

// 支持的语言列表（用于校验与 UI 切换）
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN']

