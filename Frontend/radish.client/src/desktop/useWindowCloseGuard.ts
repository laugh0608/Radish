import { useEffect } from 'react';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { useWindowStore } from '@/stores/windowStore';

export function useWindowCloseGuard(message: string | null): void {
  const currentWindow = useCurrentWindow();
  const windowId = currentWindow?.id;
  const setWindowCloseConfirmMessage = useWindowStore(state => state.setWindowCloseConfirmMessage);

  useEffect(() => {
    if (!windowId) {
      return;
    }

    setWindowCloseConfirmMessage(windowId, message);
    return () => {
      setWindowCloseConfirmMessage(windowId, null);
    };
  }, [message, setWindowCloseConfirmMessage, windowId]);
}
