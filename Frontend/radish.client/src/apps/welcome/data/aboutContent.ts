/**
 * 关于 Radish 当前阶段的内容数据
 */

export interface AboutSection {
  titleKey: string;
  contentKey: string;
  icon: string;
}

export interface Feature {
  nameKey: string;
  descriptionKey: string;
  icon: string;
  status?: 'available' | 'iterating';
}

export interface TechHighlight {
  nameKey: string;
  descriptionKey: string;
  icon: string;
}

export const aboutContent = {
  vision: {
    titleKey: 'welcome.about.vision.title',
    contentKey: 'welcome.about.vision.content',
    icon: 'mdi:compass-outline'
  } as AboutSection,

  mission: {
    titleKey: 'welcome.about.mission.title',
    contentKey: 'welcome.about.mission.content',
    icon: 'mdi:progress-wrench'
  } as AboutSection,

  features: [
    {
      nameKey: 'welcome.about.features.forum.name',
      descriptionKey: 'welcome.about.features.forum.description',
      icon: 'mdi:forum-outline',
      status: 'available'
    },
    {
      nameKey: 'welcome.about.features.docs.name',
      descriptionKey: 'welcome.about.features.docs.description',
      icon: 'mdi:book-open-page-variant-outline',
      status: 'available'
    },
    {
      nameKey: 'welcome.about.features.notificationProfile.name',
      descriptionKey: 'welcome.about.features.notificationProfile.description',
      icon: 'mdi:account-star-outline',
      status: 'available'
    },
    {
      nameKey: 'welcome.about.features.radishPit.name',
      descriptionKey: 'welcome.about.features.radishPit.description',
      icon: 'mdi:wallet-outline',
      status: 'iterating'
    },
    {
      nameKey: 'welcome.about.features.themeI18n.name',
      descriptionKey: 'welcome.about.features.themeI18n.description',
      icon: 'mdi:palette-swatch-outline',
      status: 'iterating'
    },
    {
      nameKey: 'welcome.about.features.platformEntry.name',
      descriptionKey: 'welcome.about.features.platformEntry.description',
      icon: 'mdi:transit-connection-variant',
      status: 'available'
    }
  ] as Feature[],

  techStack: [
    {
      nameKey: 'welcome.about.tech.desktop.name',
      descriptionKey: 'welcome.about.tech.desktop.description',
      icon: 'mdi:monitor-dashboard'
    },
    {
      nameKey: 'welcome.about.tech.backend.name',
      descriptionKey: 'welcome.about.tech.backend.description',
      icon: 'mdi:server-outline'
    },
    {
      nameKey: 'welcome.about.tech.frontend.name',
      descriptionKey: 'welcome.about.tech.frontend.description',
      icon: 'mdi:source-branch'
    },
    {
      nameKey: 'welcome.about.tech.consistency.name',
      descriptionKey: 'welcome.about.tech.consistency.description',
      icon: 'mdi:format-color-fill'
    }
  ] as TechHighlight[]
};
