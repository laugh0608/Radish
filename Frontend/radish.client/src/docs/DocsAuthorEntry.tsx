import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { DocsAuthorApp } from './DocsAuthorApp';

export function DocsAuthorEntry() {
  return (
    <BootstrapGate>
      <DocsAuthorApp />
    </BootstrapGate>
  );
}
