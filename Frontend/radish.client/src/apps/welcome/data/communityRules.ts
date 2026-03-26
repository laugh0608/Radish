/**
 * 欢迎页社区约定数据
 */

export interface Rule {
  titleKey: string;
  descriptionKey: string;
  severity?: 'high' | 'medium' | 'low';
}

export interface RuleCategory {
  categoryKey: string;
  icon: string;
  rules: Rule[];
}

export const communityRules: RuleCategory[] = [
  {
    categoryKey: 'welcome.rules.categories.content.title',
    icon: 'mdi:file-document-edit-outline',
    rules: [
      {
        titleKey: 'welcome.rules.content.clear.title',
        descriptionKey: 'welcome.rules.content.clear.description',
        severity: 'medium'
      },
      {
        titleKey: 'welcome.rules.content.prohibited.title',
        descriptionKey: 'welcome.rules.content.prohibited.description',
        severity: 'high'
      },
      {
        titleKey: 'welcome.rules.content.citation.title',
        descriptionKey: 'welcome.rules.content.citation.description',
        severity: 'medium'
      }
    ]
  },
  {
    categoryKey: 'welcome.rules.categories.interaction.title',
    icon: 'mdi:account-group-outline',
    rules: [
      {
        titleKey: 'welcome.rules.interaction.issueFirst.title',
        descriptionKey: 'welcome.rules.interaction.issueFirst.description',
        severity: 'high'
      },
      {
        titleKey: 'welcome.rules.interaction.evidence.title',
        descriptionKey: 'welcome.rules.interaction.evidence.description',
        severity: 'medium'
      },
      {
        titleKey: 'welcome.rules.interaction.tools.title',
        descriptionKey: 'welcome.rules.interaction.tools.description',
        severity: 'low'
      }
    ]
  },
  {
    categoryKey: 'welcome.rules.categories.security.title',
    icon: 'mdi:shield-account-outline',
    rules: [
      {
        titleKey: 'welcome.rules.security.account.title',
        descriptionKey: 'welcome.rules.security.account.description',
        severity: 'high'
      },
      {
        titleKey: 'welcome.rules.security.abuse.title',
        descriptionKey: 'welcome.rules.security.abuse.description',
        severity: 'high'
      },
      {
        titleKey: 'welcome.rules.security.boundary.title',
        descriptionKey: 'welcome.rules.security.boundary.description',
        severity: 'medium'
      }
    ]
  },
  {
    categoryKey: 'welcome.rules.categories.feedback.title',
    icon: 'mdi:message-draw',
    rules: [
      {
        titleKey: 'welcome.rules.feedback.context.title',
        descriptionKey: 'welcome.rules.feedback.context.description',
        severity: 'low'
      },
      {
        titleKey: 'welcome.rules.feedback.mismatch.title',
        descriptionKey: 'welcome.rules.feedback.mismatch.description',
        severity: 'low'
      },
      {
        titleKey: 'welcome.rules.feedback.implementation.title',
        descriptionKey: 'welcome.rules.feedback.implementation.description',
        severity: 'medium'
      }
    ]
  }
];
