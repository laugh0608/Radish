import type { TFunction } from 'i18next';

interface SystemConfigPresentationSource {
  voKey: string;
  voCategory: string;
  voName: string;
  voDescription?: string;
  voImpactSummary?: string;
}

const categoryKeyByConfigPrefix = [
  ['Site.', 'siteBranding'],
  ['UserIdentity.', 'userIdentity'],
  ['Content.', 'contentPublishing'],
  ['Comment.', 'commentInteraction'],
] as const;

function translateWithFallback(t: TFunction, key: string, fallback: string): string {
  return t(key, { defaultValue: fallback });
}

export function getSystemConfigCategoryLabel(
  configKey: string,
  fallback: string,
  t: TFunction,
): string {
  const category = categoryKeyByConfigPrefix.find(([prefix]) => configKey.startsWith(prefix));
  return category
    ? translateWithFallback(t, `systemConfig.category.${category[1]}`, fallback)
    : fallback;
}

export function getSystemConfigName(
  config: Pick<SystemConfigPresentationSource, 'voKey' | 'voName'>,
  t: TFunction,
): string {
  return translateWithFallback(
    t,
    `systemConfig.definition.${config.voKey}.name`,
    config.voName,
  );
}

export function getSystemConfigDescription(
  config: Pick<SystemConfigPresentationSource, 'voKey' | 'voDescription'>,
  t: TFunction,
): string {
  return translateWithFallback(
    t,
    `systemConfig.definition.${config.voKey}.description`,
    config.voDescription ?? '',
  );
}

export function getSystemConfigImpact(
  config: Pick<SystemConfigPresentationSource, 'voKey' | 'voImpactSummary'>,
  t: TFunction,
): string {
  return translateWithFallback(
    t,
    `systemConfig.definition.${config.voKey}.impact`,
    config.voImpactSummary ?? '',
  );
}

export function getSystemConfigPresentation(
  config: SystemConfigPresentationSource,
  t: TFunction,
) {
  return {
    category: getSystemConfigCategoryLabel(config.voKey, config.voCategory, t),
    name: getSystemConfigName(config, t),
    description: getSystemConfigDescription(config, t),
    impact: getSystemConfigImpact(config, t),
  };
}
