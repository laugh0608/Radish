import { APP_INITIALIZER, EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { DesktopThemeToggleComponent } from '../components/desktop-theme-toggle/desktop-theme-toggle.component';

function initToolbar(toolbar: ToolbarService) {
  return () => {
    const ensure = () => {
      const sub = toolbar.items$.subscribe(items => {
        const exists = Array.isArray(items) && items.some(i => i && (i as any).id === 'theme-toggle');
        if (!exists) {
          toolbar.addItem({
            id: 'theme-toggle',
            name: 'ThemeToggle',
            component: DesktopThemeToggleComponent,
            order: 2,
          });
        }
      });
      // 轻量的异步触发，尽量保证在主题初始化之后执行一次
      setTimeout(() => toolbar.addItem({
        id: 'theme-toggle',
        name: 'ThemeToggle',
        component: DesktopThemeToggleComponent,
        order: 2,
      }), 0);
      return sub;
    };
    ensure();
  };
}

export function provideAppToolbarItems(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: initToolbar,
      deps: [ToolbarService],
    },
  ]);
}
