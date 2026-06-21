import { BootstrapGate } from '@/bootstrap/BootstrapGate';
import { ShopWebApp } from './ShopWebApp';

export function ShopEntry() {
  return (
    <BootstrapGate>
      <ShopWebApp />
    </BootstrapGate>
  );
}
