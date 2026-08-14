import { useEffect, useState } from 'react';
import { Grid } from 'antd';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  BottomSheet,
  Button,
  Checkbox,
  message,
} from '@radish/ui';
import type { ClientSecretResult } from '@/types/oidc';

export interface ApplicationSecretDisclosure {
  operation: 'create' | 'rotate';
  result: ClientSecretResult;
}

interface ApplicationSecretResultProps {
  disclosure?: ApplicationSecretDisclosure;
  onClose: () => void;
}

export const ApplicationSecretResult = ({ disclosure, onClose }: ApplicationSecretResultProps) => {
  const { t } = useTranslation();
  const [acknowledged, setAcknowledged] = useState(false);
  const [copied, setCopied] = useState(false);
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.md;

  useEffect(() => {
    setAcknowledged(false);
    setCopied(false);
  }, [disclosure]);

  useEffect(() => {
    if (!disclosure) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [disclosure]);

  if (!disclosure?.result.clientSecret) return null;

  const requestClose = () => {
    if (!acknowledged) {
      message.warning(t('applications.secret.ackRequired'));
      return;
    }
    onClose();
  };

  const copySecret = async () => {
    try {
      await navigator.clipboard.writeText(disclosure.result.clientSecret ?? '');
      setCopied(true);
      message.success(t('applications.secret.copied'));
    } catch {
      message.error(t('applications.secret.copyFailed'));
    }
  };

  const title = t(disclosure.operation === 'create'
    ? 'applications.secret.createdTitle'
    : 'applications.secret.resetTitle');
  const content = (
    <div className="applications-secret-result">
      <div className="applications-secret-result__warning" role="alert">
        <strong>{t('applications.secret.oneTimeTitle')}</strong>
        <span>{t('applications.secret.oneTimeDescription')}</span>
      </div>
      <div className="applications-secret-result__field">
        <span>{t('applications.secret.clientIdLabel')}</span>
        <strong>{disclosure.result.clientId}</strong>
      </div>
      <div className="applications-secret-result__field">
        <span>{t('applications.secret.clientSecretLabel')}</span>
        <code>{disclosure.result.clientSecret}</code>
      </div>
      <Button onClick={() => void copySecret()}>{t(copied ? 'applications.secret.copiedAction' : 'applications.secret.copy')}</Button>
      <Checkbox checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)}>
        {t('applications.secret.acknowledge')}
      </Checkbox>
    </div>
  );

  if (isMobile) {
    return (
      <BottomSheet
        isOpen
        onClose={requestClose}
        closeLabel={t('applications.secret.close')}
        title={title}
        height="auto"
        closeOnOverlayClick={false}
        closeOnEscape={false}
        className="applications-secret-sheet"
        footer={(
          <Button variant="primary" disabled={!acknowledged} onClick={requestClose}>
            {t('applications.secret.close')}
          </Button>
        )}
      >
        {content}
      </BottomSheet>
    );
  }

  return (
    <Modal
      title={title}
      open
      onCancel={requestClose}
      closable={acknowledged}
      maskClosable={false}
      keyboard={false}
      width={640}
      footer={(
        <Button variant="primary" disabled={!acknowledged} onClick={requestClose}>
          {t('applications.secret.close')}
        </Button>
      )}
    >
      {content}
    </Modal>
  );
};
