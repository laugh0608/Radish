export type Locale = 'en' | 'zh-CN'

export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN']

export const messages: Record<Locale, Record<string, string>> = {
  en: {
    'lang.en': 'English',
    'lang.zhCN': '简体中文',
    'aria.langSwitcher': 'Language switcher',

    // App copies (demo)
    'app.welcome': 'Welcome',
    'app.getStarted': 'Getting Started',
    'app.tutorial': 'Web Application Development Tutorial',
    'app.exploreTutorial': 'Explore Tutorial',
  },
  'zh-CN': {
    'lang.en': 'English',
    'lang.zhCN': '简体中文',
    'aria.langSwitcher': '语言切换',

    // App copies (demo)
    'app.welcome': '欢迎',
    'app.getStarted': '快速上手',
    'app.tutorial': 'Web 应用开发教程',
    'app.exploreTutorial': '查看教程',
  },
}

