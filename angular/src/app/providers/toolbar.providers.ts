import { APP_INITIALIZER, EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { DesktopThemeToggleComponent } from '../components/desktop-theme-toggle/desktop-theme-toggle.component';

// 全局去重开关：防止同一生命周期内多次并发注入
let pendingAdd = false;

function initToolbar(toolbar: ToolbarService, router: Router) {
  return () => {
    const hasDom = () => !!document.querySelector('lpx-toolbar-items app-desktop-theme-toggle, app-desktop-theme-toggle');
    const hasInList = (items: unknown[]) => Array.isArray(items)
      && items.some((i: any) => i && (i.id === 'theme-toggle' || i.name === 'ThemeToggle'));

    const tryAddOnce = () => {
      if (pendingAdd) return;
      if (hasDom()) return; // 已存在，不再添加
      pendingAdd = true; // 立刻标记，防止并发 schedule
      setTimeout(() => {
        try {
          if (!hasDom()) {
            toolbar.addItem({
              id: 'theme-toggle',
              name: 'ThemeToggle',
              component: DesktopThemeToggleComponent,
              order: 2,
            });
          }
        } finally {
          pendingAdd = false;
        }
      }, 0);
    };

    // 1) 首屏渲染后调一次（双 rAF 确保顶栏节点已挂载）
    requestAnimationFrame(() => requestAnimationFrame(() => tryAddOnce()));

    // 2) toolbar 列表变化：缺失且 DOM 不存在则补一次
    const subA = toolbar.items$.subscribe(items => {
      if (!hasInList(items as any) && !hasDom()) tryAddOnce();
    });

    // 3) 路由完成后：若 DOM 中不存在则补一次
    const subB = router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => setTimeout(() => { if (!hasDom()) tryAddOnce(); }, 0));

    void subA; void subB; // 安静订阅
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
