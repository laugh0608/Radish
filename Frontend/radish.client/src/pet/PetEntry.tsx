import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { PetApp } from './PetApp';

export function PetEntry() {
  return (
    <BootstrapGate>
      <PetApp />
    </BootstrapGate>
  );
}
