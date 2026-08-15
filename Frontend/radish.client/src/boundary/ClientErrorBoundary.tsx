import { Component, type ErrorInfo, type ReactNode } from 'react';
import { WebStateSlot } from '@/components/web-shell';
import i18n from '@/i18n';
import { log } from '@/utils/logger';
import styles from './BoundaryPage.module.css';

interface ClientErrorBoundaryProps {
  children: ReactNode;
}

interface ClientErrorBoundaryState {
  error: Error | null;
  diagnosticId: string | null;
}

function createDiagnosticId(): string {
  const timestamp = Date.now().toString(36).toUpperCase();
  const randomPart = Math.random().toString(36).slice(2, 8).toUpperCase();
  return `WEB-${timestamp}-${randomPart}`;
}

export class ClientErrorBoundary extends Component<
  ClientErrorBoundaryProps,
  ClientErrorBoundaryState
> {
  state: ClientErrorBoundaryState = {
    error: null,
    diagnosticId: null,
  };

  static getDerivedStateFromError(error: Error): Partial<ClientErrorBoundaryState> {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    const diagnosticId = createDiagnosticId();

    log.error('ClientBoundary', 'Client runtime boundary captured an error', {
      diagnosticId,
      pathname: window.location.pathname,
      error,
      componentStack: errorInfo.componentStack,
    });

    document.title = `${i18n.t('boundary.runtime.documentTitle')} · Radish`;
    this.setState({ diagnosticId });
  }

  private handleRetry = (): void => {
    this.setState({ error: null, diagnosticId: null });
  };

  private handleReload = (): void => {
    window.location.reload();
  };

  private handleGoHome = (): void => {
    window.location.assign('/discover');
  };

  render(): ReactNode {
    const { children } = this.props;
    const { error, diagnosticId } = this.state;

    if (!error) {
      return children;
    }

    return (
      <main className={styles.page}>
        <WebStateSlot
          className={styles.panel}
          tone="error"
          title={i18n.t('boundary.runtime.title')}
          description={i18n.t('boundary.runtime.description')}
          meta={diagnosticId
            ? i18n.t('boundary.runtime.diagnostic', { id: diagnosticId })
            : undefined}
          actions={[
            {
              label: i18n.t('boundary.runtime.retry'),
              onClick: this.handleRetry,
            },
            {
              label: i18n.t('boundary.runtime.reload'),
              onClick: this.handleReload,
              kind: 'secondary',
            },
            {
              label: i18n.t('boundary.runtime.home'),
              onClick: this.handleGoHome,
              kind: 'secondary',
            },
          ]}
        />
      </main>
    );
  }
}
