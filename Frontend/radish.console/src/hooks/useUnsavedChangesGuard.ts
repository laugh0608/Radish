import { useEffect } from 'react';
import { useBlocker } from 'react-router';

export function useUnsavedChangesGuard(locked: boolean, confirmMessage: string) {
  const blocker = useBlocker(locked);

  useEffect(() => {
    if (blocker.state !== 'blocked') {
      return;
    }

    if (window.confirm(confirmMessage)) {
      blocker.proceed();
      return;
    }

    blocker.reset();
  }, [blocker, confirmMessage]);

  useEffect(() => {
    if (!locked) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [locked]);
}
