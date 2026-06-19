import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { CircleApp } from './CircleApp';

export function CircleEntry() {
  return (
    <BootstrapGate>
      <CircleApp />
    </BootstrapGate>
  );
}
