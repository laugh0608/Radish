import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { MeApp } from './MeApp';

export function MeEntry() {
  return (
    <BootstrapGate>
      <MeApp />
    </BootstrapGate>
  );
}
