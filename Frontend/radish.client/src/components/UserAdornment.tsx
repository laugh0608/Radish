import type { UserAdornment as UserAdornmentValue } from '@/types/userAdornment';
import { resolveMediaUrl } from '@/utils/media';
import styles from './UserAdornment.module.css';

interface UserAdornmentProps {
  adornment?: UserAdornmentValue | null;
  density?: 'compact' | 'regular';
}

export const UserAdornment = ({ adornment, density = 'compact' }: UserAdornmentProps) => {
  const badge = adornment?.voBadge;
  const title = adornment?.voTitle;
  const badgeImageUrl = resolveMediaUrl(badge?.voImageUrl);

  if ((!badge || !badgeImageUrl) && !title?.voName?.trim()) {
    return null;
  }

  return (
    <span className={`${styles.adornment} ${density === 'regular' ? styles.regular : styles.compact}`}>
      {badge && badgeImageUrl ? (
        <span className={styles.badge} title={badge.voName} data-resource-key={badge.voResourceKey}>
          <img src={badgeImageUrl} alt={badge.voName} loading="lazy" />
        </span>
      ) : null}
      {title?.voName?.trim() ? (
        <span className={styles.title} title={title.voName} data-resource-key={title.voResourceKey}>
          {title.voName}
        </span>
      ) : null}
    </span>
  );
};
