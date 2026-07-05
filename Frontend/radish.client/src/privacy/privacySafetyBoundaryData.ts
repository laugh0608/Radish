export type PrivacyBoundaryAudience = 'public' | 'private' | 'console' | 'restricted';

export interface PrivacyBoundaryEntry {
  id: string;
  icon: string;
  audience: PrivacyBoundaryAudience;
  titleKey: string;
  descriptionKey: string;
  exampleKeys: string[];
}

export interface SafetyResponseEntry {
  id: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
}

export const privacyBoundaryAudienceOrder: PrivacyBoundaryAudience[] = [
  'public',
  'private',
  'console',
  'restricted',
];

export const privacyBoundaryEntries: PrivacyBoundaryEntry[] = [
  {
    id: 'public-community',
    icon: 'mdi:earth',
    audience: 'public',
    titleKey: 'privacySafety.boundary.public.title',
    descriptionKey: 'privacySafety.boundary.public.description',
    exampleKeys: [
      'privacySafety.boundary.public.example.profile',
      'privacySafety.boundary.public.example.content',
      'privacySafety.boundary.public.example.shop',
    ],
  },
  {
    id: 'private-revisit',
    icon: 'mdi:account-lock-outline',
    audience: 'private',
    titleKey: 'privacySafety.boundary.private.title',
    descriptionKey: 'privacySafety.boundary.private.description',
    exampleKeys: [
      'privacySafety.boundary.private.example.assets',
      'privacySafety.boundary.private.example.attachments',
      'privacySafety.boundary.private.example.history',
    ],
  },
  {
    id: 'console-governance',
    icon: 'mdi:shield-account-outline',
    audience: 'console',
    titleKey: 'privacySafety.boundary.console.title',
    descriptionKey: 'privacySafety.boundary.console.description',
    exampleKeys: [
      'privacySafety.boundary.console.example.reports',
      'privacySafety.boundary.console.example.orders',
      'privacySafety.boundary.console.example.audit',
    ],
  },
  {
    id: 'restricted-data',
    icon: 'mdi:lock-alert-outline',
    audience: 'restricted',
    titleKey: 'privacySafety.boundary.restricted.title',
    descriptionKey: 'privacySafety.boundary.restricted.description',
    exampleKeys: [
      'privacySafety.boundary.restricted.example.credentials',
      'privacySafety.boundary.restricted.example.contact',
      'privacySafety.boundary.restricted.example.thirdParty',
    ],
  },
];

export const safetyResponseEntries: SafetyResponseEntry[] = [
  {
    id: 'report-context',
    icon: 'mdi:flag-outline',
    titleKey: 'privacySafety.response.report.title',
    descriptionKey: 'privacySafety.response.report.description',
  },
  {
    id: 'preserve-evidence',
    icon: 'mdi:clipboard-text-clock-outline',
    titleKey: 'privacySafety.response.evidence.title',
    descriptionKey: 'privacySafety.response.evidence.description',
  },
  {
    id: 'account-assets',
    icon: 'mdi:wallet-lock-outline',
    titleKey: 'privacySafety.response.assets.title',
    descriptionKey: 'privacySafety.response.assets.description',
  },
  {
    id: 'urgent-risk',
    icon: 'mdi:shield-alert-outline',
    titleKey: 'privacySafety.response.urgent.title',
    descriptionKey: 'privacySafety.response.urgent.description',
  },
];
