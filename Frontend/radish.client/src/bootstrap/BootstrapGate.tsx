import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { getBootstrapStatus, createFirstAdministrator } from '@/api/bootstrap';
import { redirectToLogin } from '@/services/auth';
import { log } from '@/utils/logger';
import styles from './BootstrapGate.module.css';

type BootstrapGateState = 'loading' | 'ready' | 'required' | 'error';

interface BootstrapGateProps {
  children: ReactNode;
}

export function BootstrapGate({ children }: BootstrapGateProps) {
  const [state, setState] = useState<BootstrapGateState>('loading');
  const [error, setError] = useState<string>();

  const loadStatus = useCallback(async () => {
    setState('loading');
    setError(undefined);

    try {
      const response = await getBootstrapStatus();
      if (!response.ok || !response.data) {
        setState('error');
        setError(response.message || '无法读取系统初始化状态');
        return;
      }

      setState(response.data.voRequiresAdminInitialization ? 'required' : 'ready');
    } catch (err) {
      log.error('bootstrap', '读取系统初始化状态失败', err);
      setState('error');
      setError(err instanceof Error ? err.message : '无法读取系统初始化状态');
    }
  }, []);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  if (state === 'ready') {
    return <>{children}</>;
  }

  if (state === 'required') {
    return <FirstAdminBootstrapPage />;
  }

  if (state === 'error') {
    return (
      <div className={styles.page}>
        <section className={styles.panel}>
          <p className={styles.kicker}>系统初始化</p>
          <h1>无法确认初始化状态</h1>
          <p className={styles.description}>{error}</p>
          <button type="button" className={styles.primaryButton} onClick={() => void loadStatus()}>
            重试
          </button>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <section className={styles.panel}>
        <p className={styles.kicker}>系统初始化</p>
        <h1>正在检查初始化状态</h1>
        <p className={styles.description}>请稍候。</p>
      </section>
    </div>
  );
}

function FirstAdminBootstrapPage() {
  const [loginName, setLoginName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string>();
  const [createdLoginName, setCreatedLoginName] = useState<string>();

  useEffect(() => {
    document.title = '初始化管理员';
  }, []);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setMessage(undefined);

    try {
      const response = await createFirstAdministrator({
        loginName,
        email,
        password,
        confirmPassword,
      });

      if (!response.ok || !response.data) {
        setMessage(response.message || '初始化失败');
        return;
      }

      setCreatedLoginName(response.data.voLoginName);
    } catch (err) {
      log.error('bootstrap', '创建首个管理员失败', err);
      setMessage(err instanceof Error ? err.message : '初始化失败');
    } finally {
      setSubmitting(false);
    }
  };

  if (createdLoginName) {
    return (
      <div className={styles.page}>
        <section className={styles.panel}>
          <p className={styles.kicker}>系统初始化</p>
          <h1>管理员已创建</h1>
          <p className={styles.description}>使用账号 {createdLoginName} 登录后即可进入工作台和管理入口。</p>
          <button type="button" className={styles.primaryButton} onClick={redirectToLogin}>
            登录
          </button>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <section className={styles.panel}>
        <p className={styles.kicker}>首次部署</p>
        <h1>创建首个管理员</h1>
        <p className={styles.description}>
          当前系统尚无管理员账号。请设置一个新的管理员账号和强密码，初始化完成后默认账号入口会关闭。
        </p>

        <form className={styles.form} onSubmit={handleSubmit}>
          <label className={styles.field}>
            <span>登录账号</span>
            <input
              value={loginName}
              onChange={(event) => setLoginName(event.target.value)}
              autoComplete="username"
              minLength={3}
              maxLength={50}
              required
            />
          </label>

          <label className={styles.field}>
            <span>邮箱</span>
            <input
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              type="email"
            />
          </label>

          <label className={styles.field}>
            <span>密码</span>
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
            <span>确认密码</span>
            <input
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              autoComplete="new-password"
              type="password"
              minLength={12}
              required
            />
          </label>

          <p className={styles.passwordRule}>密码至少 12 位，并包含大写字母、小写字母、数字和特殊字符。</p>

          {message && <p className={styles.error}>{message}</p>}

          <button type="submit" className={styles.primaryButton} disabled={submitting}>
            {submitting ? '正在初始化...' : '创建管理员'}
          </button>
        </form>
      </section>
    </div>
  );
}
