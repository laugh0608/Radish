import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { NotificationsApp } from './NotificationsApp';

export function NotificationsEntry() {
  return (
    <BootstrapGate>
      <NotificationsApp />
    </BootstrapGate>
  );
}
