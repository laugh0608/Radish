import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { PublicPetCard as PublicPetCardData } from '@/api/user';
import {
  resolvePetGrowthStageTranslationKey,
  resolvePetMoodTranslationKey,
  resolvePetShapeTranslationKey,
  resolvePetSpeciesTranslationKey,
} from '@/pet/petPresentation';
import styles from './PublicPetCard.module.css';

interface PublicPetCardProps {
  pet: PublicPetCardData;
}

interface PublicPetMetadataItem {
  key: 'species' | 'shape' | 'stage' | 'mood';
  icon: string;
  label: string;
  value: string;
}

export const PublicPetCard = ({ pet }: PublicPetCardProps) => {
  const { t } = useTranslation();
  const titleId = useId();
  const descriptionId = useId();
  const metadata: PublicPetMetadataItem[] = [
    {
      key: 'species',
      icon: 'mdi:leaf',
      label: t('pet.public.speciesLabel'),
      value: t(resolvePetSpeciesTranslationKey(pet.voSpeciesKey)),
    },
    {
      key: 'shape',
      icon: 'mdi:sprout-outline',
      label: t('pet.public.shapeLabel'),
      value: t(resolvePetShapeTranslationKey(pet.voShapeKey)),
    },
    {
      key: 'stage',
      icon: 'mdi:chart-timeline-variant-shimmer',
      label: t('pet.public.stageLabel'),
      value: t(resolvePetGrowthStageTranslationKey(pet.voGrowthStage)),
    },
    {
      key: 'mood',
      icon: 'mdi:emoticon-outline',
      label: t('pet.public.moodLabel'),
      value: t(resolvePetMoodTranslationKey(pet.voMood)),
    },
  ];

  return (
    <section
      className={styles.card}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
    >
      <div className={styles.portrait} data-mood={pet.voMood} aria-hidden="true">
        <Icon icon="mdi:sprout" size={34} />
        <span>{t('pet.public.portraitMark')}</span>
      </div>

      <div className={styles.identity}>
        <p className={styles.kicker}>{t('pet.public.kicker')}</p>
        <div className={styles.titleRow}>
          <h2 id={titleId} className={styles.name}>{pet.voName}</h2>
          <span className={styles.publicBadge}>
            <Icon icon="mdi:eye-outline" size={15} aria-hidden="true" />
            {t('pet.public.visibleBadge')}
          </span>
        </div>
      </div>

      <p id={descriptionId} className={styles.description}>
        {t('pet.public.description')}
      </p>

      <dl className={styles.metadata}>
        {metadata.map((item) => (
          <div className={styles.metadataItem} key={item.key}>
            <dt className={styles.metadataLabel}>
              <Icon icon={item.icon} size={15} aria-hidden="true" />
              <span>{item.label}</span>
            </dt>
            <dd className={styles.metadataValue}>{item.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
};
