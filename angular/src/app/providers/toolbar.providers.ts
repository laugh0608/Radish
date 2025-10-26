import { APP_INITIALIZER, EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { DesktopThemeToggleComponent } from '../components/desktop-theme-toggle/desktop-theme-toggle.component';

function initToolbar(toolbar: ToolbarService) {
  return () => {
    // 在 PC 端顶部右侧添加“明暗主题切换”按钮
    toolbar.addItem({
      id: 'theme-toggle',
      name: 'ThemeToggle',
      component: DesktopThemeToggleComponent,
      order: 9999,
    });
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

