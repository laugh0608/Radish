import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { ApiResponseError } from '@radish/http';
import { useTranslation } from 'react-i18next';
import { getBootstrapStatus, createFirstAdministrator } from '@/api/bootstrap';
import { redirectToLogin } from '@/services/auth';
import { log } from '@/utils/logger';
import styles from './BootstrapGate.module.css';

type BootstrapGateState = 'loading' | 'ready' | 'required' | 'error';

interface BootstrapGateProps {
  children: ReactNode;
}

export function BootstrapGate({ children }: BootstrapGateProps) {
  const { t, i18n } = useTranslation();
  const [state, setState] = useState<BootstrapGateState>('loading');

  const loadStatus = useCallback(async () => {
    setState('loading');

    try {
      const status = await getBootstrapStatus(i18n.t('bootstrap.error.statusFallback'));
      setState(status.voRequiresAdminInitialization ? 'required' : 'ready');
    } catch (err) {
      log.error('bootstrap', '读取系统初始化状态失败', err);
      setState('error');
    }
  }, [i18n]);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  useEffect(() => {
    if (state === 'loading') {
      document.title = t('bootstrap.documentTitle.checking');
    } else if (state === 'error') {
      document.title = t('bootstrap.documentTitle.error');
    }
  }, [state, t]);

  if (state === 'ready') {
    return <>{children}</>;
  }

  if (state === 'required') {
    return <FirstAdminBootstrapPage onInitializationUnavailable={loadStatus} />;
  }

  if (state === 'error') {
    return (
      <div className={styles.page}>
        <section className={styles.panel}>
          <p className={styles.kicker}>{t('bootstrap.kicker.systemInitialization')}</p>
          <h1>{t('bootstrap.status.errorTitle')}</h1>
          <p className={styles.description}>{t('bootstrap.error.statusFallback')}</p>
          <button type="button" className={styles.primaryButton} onClick={() => void loadStatus()}>
            {t('common.retry')}
          </button>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <section className={styles.panel}>
        <p className={styles.kicker}>{t('bootstrap.kicker.systemInitialization')}</p>
        <h1>{t('bootstrap.status.checkingTitle')}</h1>
        <p className={styles.description}>{t('bootstrap.status.checkingDescription')}</p>
      </section>
    </div>
  );
}

interface FirstAdminBootstrapPageProps {
  onInitializationUnavailable: () => Promise<void>;
}

function FirstAdminBootstrapPage({ onInitializationUnavailable }: FirstAdminBootstrapPageProps) {
  const { t, i18n } = useTranslation();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [messageKey, setMessageKey] = useState<string>();
  const [createdAdmin, setCreatedAdmin] = useState<{ displayName: string; email: string }>();

  useEffect(() => {
    document.title = createdAdmin
      ? t('bootstrap.documentTitle.completed')
      : t('bootstrap.documentTitle.required');
  }, [createdAdmin, t]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setMessageKey(undefined);

    try {
      const response = await createFirstAdministrator(
        {
          displayName,
          email,
          password,
          confirmPassword,
        },
        t('bootstrap.error.createFallback'),
      );

      setCreatedAdmin({
        displayName: response.voDisplayName,
        email: response.voEmail,
      });
    } catch (err) {
      log.error('bootstrap', '创建首个管理员失败', err);
      if (err instanceof ApiResponseError && err.code === 'Bootstrap.AlreadyInitialized') {
        await onInitializationUnavailable();
        return;
      }

      const stableMessageKey = err instanceof ApiResponseError
        && err.messageKey?.startsWith('error.bootstrap.')
        && i18n.exists(err.messageKey)
        ? err.messageKey
        : 'bootstrap.error.createFallback';
      setMessageKey(stableMessageKey);
    } finally {
      setSubmitting(false);
    }
  };

  if (createdAdmin) {
    return (
      <div className={styles.page}>
        <section className={styles.panel}>
          <p className={styles.kicker}>{t('bootstrap.kicker.systemInitialization')}</p>
          <h1>{t('bootstrap.completed.title')}</h1>
          <p className={styles.description}>
            {t('bootstrap.completed.description', {
              displayName: createdAdmin.displayName,
              email: createdAdmin.email,
            })}
          </p>
          <button type="button" className={styles.primaryButton} onClick={() => redirectToLogin()}>
            {t('bootstrap.action.login')}
          </button>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <section className={styles.panel}>
        <p className={styles.kicker}>{t('bootstrap.kicker.firstDeployment')}</p>
        <h1>{t('bootstrap.form.title')}</h1>
        <p className={styles.description}>
          {t('bootstrap.form.description')}
        </p>

        <form className={styles.form} onSubmit={handleSubmit}>
          <label className={styles.field}>
            <span>{t('bootstrap.field.displayName')}</span>
            <input
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              autoComplete="nickname"
              minLength={2}
              maxLength={24}
              required
            />
            <span className={styles.fieldHint}>{t('bootstrap.field.displayNameHint')}</span>
          </label>

          <label className={styles.field}>
            <span>{t('bootstrap.field.email')}</span>
            <input
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              type="email"
              required
            />
          </label>

          <label className={styles.field}>
            <span>{t('bootstrap.field.password')}</span>
            <input
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="new-password"
              type="password"
              minLength={12}
              required
            />
          </label>

          <label className={styles.field}>
            <span>{t('bootstrap.field.confirmPassword')}</span>
            <input
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              autoComplete="new-password"
              type="password"
              minLength={12}
              required
            />
          </label>

          <p className={styles.passwordRule}>{t('bootstrap.form.passwordRule')}</p>

          {messageKey && <p className={styles.error}>{t(messageKey)}</p>}

          <button type="submit" className={styles.primaryButton} disabled={submitting}>
            {submitting ? t('bootstrap.action.creating') : t('bootstrap.action.create')}
          </button>
        </form>
      </section>
    </div>
  );
}
