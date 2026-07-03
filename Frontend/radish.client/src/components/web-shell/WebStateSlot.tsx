import type { MouseEvent, ReactNode } from 'react';
import { Icon } from '@radish/ui/icon';
import styles from './WebStateSlot.module.css';

export type WebStateSlotTone = 'loading' | 'empty' | 'error' | 'notFound' | 'permission' | 'auth' | 'info';

export interface WebStateSlotAction {
  label: string;
  href?: string;
  onClick?: (event: MouseEvent<HTMLAnchorElement | HTMLButtonElement>) => void;
  kind?: 'primary' | 'secondary';
}

interface WebStateSlotProps {
  tone: WebStateSlotTone;
  title: string;
  description: string;
  icon?: string;
  compact?: boolean;
  actions?: WebStateSlotAction[];
  meta?: ReactNode;
  className?: string;
}

function resolveIcon(tone: WebStateSlotTone): string {
  switch (tone) {
    case 'loading':
      return 'mdi:progress-clock';
    case 'empty':
      return 'mdi:text-box-search-outline';
    case 'notFound':
      return 'mdi:file-search-outline';
    case 'permission':
      return 'mdi:shield-lock-outline';
    case 'auth':
      return 'mdi:account-clock-outline';
    case 'info':
      return 'mdi:information-outline';
    default:
      return 'mdi:alert-circle-outline';
  }
}

export function WebStateSlot({
  tone,
  title,
  description,
  icon,
  compact = false,
  actions = [],
  meta,
  className,
}: WebStateSlotProps) {
  const rootClassName = [
    styles.slot,
    compact ? styles.compact : '',
    className ?? '',
  ].filter(Boolean).join(' ');
  const resolvedIcon = icon ?? resolveIcon(tone);

  return (
    <section className={rootClassName} data-tone={tone} aria-live={tone === 'loading' ? 'polite' : undefined}>
      <div className={styles.iconWrap}>
        <Icon icon={resolvedIcon} size={compact ? 18 : 22} className={tone === 'loading' ? styles.spin : undefined} />
      </div>
      <div className={styles.body}>
        <h2 className={styles.title}>{title}</h2>
        <p className={styles.description}>{description}</p>
        {meta ? <div className={styles.meta}>{meta}</div> : null}
        {actions.length > 0 ? (
          <div className={styles.actions}>
            {actions.map((action) => {
              const actionClassName = action.kind === 'secondary' ? styles.secondaryAction : styles.primaryAction;
              return action.href ? (
                <a key={`${action.label}:${action.href}`} className={actionClassName} href={action.href} onClick={action.onClick}>
                  {action.label}
                </a>
              ) : (
                <button key={action.label} type="button" className={actionClassName} onClick={action.onClick}>
                  {action.label}
                </button>
              );
            })}
          </div>
        ) : null}
      </div>
    </section>
  );
}
