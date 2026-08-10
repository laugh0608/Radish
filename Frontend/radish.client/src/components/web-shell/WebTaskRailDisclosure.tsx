import { useId, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import styles from './WebTaskRailDisclosure.module.css';

export interface WebTaskRailDisclosureProps {
  label: string;
  summary?: string;
  children: ReactNode;
  defaultExpanded?: boolean;
}

export const WebTaskRailDisclosure = ({
  label,
  summary,
  children,
  defaultExpanded = false,
}: WebTaskRailDisclosureProps) => {
  const { t } = useTranslation();
  const contentId = useId();
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className={styles.root} data-expanded={expanded ? 'true' : 'false'}>
      <button
        type="button"
        className={styles.toggle}
        aria-controls={contentId}
        aria-expanded={expanded}
        onClick={() => setExpanded((current) => !current)}
      >
        <span>
          <strong>{label}</strong>
          {summary ? <small>{summary}</small> : null}
        </span>
        <span className={styles.toggleAction}>
          {t(expanded ? 'privateTaskRail.collapse' : 'privateTaskRail.expand')}
          <Icon icon={expanded ? 'mdi:chevron-up' : 'mdi:chevron-down'} size={19} />
        </span>
      </button>
      <div className={styles.content} id={contentId}>
        {children}
      </div>
    </div>
  );
};
