import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { Shell } from './Shell';

export function RootEntry() {
  return (
    <BootstrapGate>
      <Shell />
    </BootstrapGate>
  );
}
