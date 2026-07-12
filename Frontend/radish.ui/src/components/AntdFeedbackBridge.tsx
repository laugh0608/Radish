import { App } from 'antd';
import type { ReactNode } from 'react';
import { useLayoutEffect } from 'react';
import { clearScopedFeedbackApis, setScopedFeedbackApis } from './feedbackApi';

export interface AntdFeedbackBridgeProps {
  children?: ReactNode;
}

export function AntdFeedbackBridge({ children }: AntdFeedbackBridgeProps) {
  const appApis = App.useApp();

  useLayoutEffect(() => {
    setScopedFeedbackApis(appApis);

    return () => {
      clearScopedFeedbackApis(appApis);
    };
  }, [appApis]);

  return <>{children}</>;
}
