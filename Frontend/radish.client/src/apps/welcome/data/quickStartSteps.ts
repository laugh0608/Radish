/**
 * 欢迎页快速上手指南数据
 */

export interface QuickStartStep {
  titleKey: string;
  descriptionKey: string;
  icon: string;
}

export interface QuickStartCategory {
  categoryKey: string;
  icon: string;
  steps: QuickStartStep[];
}

export const quickStartSteps: QuickStartCategory[] = [
  {
    categoryKey: 'welcome.quickStart.categories.desktop.title',
    icon: 'mdi:monitor-cellphone',
    steps: [
      {
        titleKey: 'welcome.quickStart.desktop.openApp.title',
        descriptionKey: 'welcome.quickStart.desktop.openApp.description',
        icon: 'mdi:cursor-default-click-outline'
      },
      {
        titleKey: 'welcome.quickStart.desktop.useDock.title',
        descriptionKey: 'welcome.quickStart.desktop.useDock.description',
        icon: 'mdi:dock-top'
      },
      {
        titleKey: 'welcome.quickStart.desktop.windowControl.title',
        descriptionKey: 'welcome.quickStart.desktop.windowControl.description',
        icon: 'mdi:window-restore'
      },
      {
        titleKey: 'welcome.quickStart.desktop.switchThemeLanguage.title',
        descriptionKey: 'welcome.quickStart.desktop.switchThemeLanguage.description',
        icon: 'mdi:translate-variant'
      }
    ]
  },
  {
    categoryKey: 'welcome.quickStart.categories.apps.title',
    icon: 'mdi:apps-box',
    steps: [
      {
        titleKey: 'welcome.quickStart.apps.forum.title',
        descriptionKey: 'welcome.quickStart.apps.forum.description',
        icon: 'mdi:forum-outline'
      },
      {
        titleKey: 'welcome.quickStart.apps.docs.title',
        descriptionKey: 'welcome.quickStart.apps.docs.description',
        icon: 'mdi:book-open-outline'
      },
      {
        titleKey: 'welcome.quickStart.apps.notifications.title',
        descriptionKey: 'welcome.quickStart.apps.notifications.description',
        icon: 'mdi:bell-outline'
      },
      {
        titleKey: 'welcome.quickStart.apps.profile.title',
        descriptionKey: 'welcome.quickStart.apps.profile.description',
        icon: 'mdi:account-badge-outline'
      }
    ]
  },
  {
    categoryKey: 'welcome.quickStart.categories.route.title',
    icon: 'mdi:route',
    steps: [
      {
        titleKey: 'welcome.quickStart.route.radishPit.title',
        descriptionKey: 'welcome.quickStart.route.radishPit.description',
        icon: 'mdi:wallet-outline'
      },
      {
        titleKey: 'welcome.quickStart.route.showcase.title',
        descriptionKey: 'welcome.quickStart.route.showcase.description',
        icon: 'mdi:view-grid-plus-outline'
      },
      {
        titleKey: 'welcome.quickStart.route.console.title',
        descriptionKey: 'welcome.quickStart.route.console.description',
        icon: 'mdi:application-cog-outline'
      }
    ]
  },
  {
    categoryKey: 'welcome.quickStart.categories.issue.title',
    icon: 'mdi:lifebuoy',
    steps: [
      {
        titleKey: 'welcome.quickStart.issue.visibleEntry.title',
        descriptionKey: 'welcome.quickStart.issue.visibleEntry.description',
        icon: 'mdi:map-marker-path'
      },
      {
        titleKey: 'welcome.quickStart.issue.docs.title',
        descriptionKey: 'welcome.quickStart.issue.docs.description',
        icon: 'mdi:file-document-outline'
      },
      {
        titleKey: 'welcome.quickStart.issue.feedback.title',
        descriptionKey: 'welcome.quickStart.issue.feedback.description',
        icon: 'mdi:message-alert-outline'
      }
    ]
  }
];
