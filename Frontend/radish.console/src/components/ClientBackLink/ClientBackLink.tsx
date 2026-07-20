import { useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { LeftOutlined } from '@radish/ui';
import {
  clearRememberedClientBackTo,
  getRememberedClientBackTo,
  resolveClientBackLabelKey,
} from '@/utils/clientNavigation';
import './ClientBackLink.css';

export function ClientBackLink() {
  const { t } = useTranslation();
  const [isReturning, setIsReturning] = useState(false);
  const clientBackTo = getRememberedClientBackTo() ?? '/workbench';
  const label = t(resolveClientBackLabelKey(clientBackTo));

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (
      event.button !== 0
      || event.metaKey
      || event.ctrlKey
      || event.shiftKey
      || event.altKey
    ) {
      return;
    }

    event.preventDefault();
    setIsReturning(true);
    clearRememberedClientBackTo();
    window.requestAnimationFrame(() => {
      window.location.assign(clientBackTo);
    });
  };

  return (
    <a
      className={[
        'console-client-back-link',
        isReturning ? 'console-client-back-link--pending' : '',
      ].filter(Boolean).join(' ')}
      href={clientBackTo}
      aria-busy={isReturning}
      onClick={handleClick}
    >
      <LeftOutlined />
      <span>{isReturning ? t('console.clientBack.returning') : label}</span>
    </a>
  );
}
