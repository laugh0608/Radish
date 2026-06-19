import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { MessagesApp } from './MessagesApp';

export function MessagesEntry() {
  return (
    <BootstrapGate>
      <MessagesApp />
    </BootstrapGate>
  );
}
