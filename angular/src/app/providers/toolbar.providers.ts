import { APP_INITIALIZER, EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { DesktopThemeToggleComponent } from '../components/desktop-theme-toggle/desktop-theme-toggle.component';

function initToolbar(toolbar: ToolbarService, router: Router) {
  return () => {
    // 以 ToolbarService 的 items 列表为唯一真相做去重，避免基于 DOM 的竞争条件
    let adding = false;
    const ensure = (items: unknown[]) => {
      if (adding) return;
      const exists = Array.isArray(items) && items.some((i: any) => i && (i.id === 'theme-toggle' || i.name === 'ThemeToggle'));
      if (!exists) {
        adding = true;
        setTimeout(() => {
          try {
            toolbar.addItem({
              id: 'theme-toggle',
              name: 'ThemeToggle',
              component: DesktopThemeToggleComponent,
              order: 2,
            });
          } finally {
            adding = false;
          }
        }, 0);
      }
    };

    // 1) 监听 toolbar 列表变更，缺失则补一次
    const subA = toolbar.items$.subscribe(items => ensure(items as any));

    // 2) 路由完成后，如果主题重建导致列表清空，再补一次
    const subB = router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => setTimeout(() => ensure((toolbar as any)._items ?? []), 0));

    // 注意：APP_INITIALIZER 不需返回解除逻辑；如需可在根组件销毁时清理。
    void subA; void subB;
  };
}

export function provideAppToolbarItems(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: initToolbar,
      deps: [ToolbarService, Router],
    },
  ]);
}
