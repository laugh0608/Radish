import { APP_INITIALIZER, EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { DesktopThemeToggleComponent } from '../components/desktop-theme-toggle/desktop-theme-toggle.component';

function initToolbar(toolbar: ToolbarService, router: Router) {
  return () => {
    // DOM 级去重：确保顶栏里不存在我们的组件再尝试添加
    // 视为“已有主题切换器”的条件：
    // - 我们自定义的 app-desktop-theme-toggle 已存在
    // - 主题内置的切换器（通常是含有 bi-moon/bi-sun 图标的 dropdown-toggle）存在
    const hasDom = () => !!document.querySelector(
      'lpx-toolbar-items app-desktop-theme-toggle, app-desktop-theme-toggle, ' +
      '.lpx-topbar .lpx-toolbar .dropdown-toggle i.bi-moon, .lpx-topbar .lpx-toolbar .dropdown-toggle i.bi-sun'
    );

    // 轻量防抖，避免同一时序重复 addItem
    let addTimer: any = null;
    const debouncedAdd = () => {
      if (addTimer) return;
      addTimer = setTimeout(() => {
        addTimer = null;
        if (!hasDom()) {
          toolbar.addItem({
            id: 'theme-toggle',
            name: 'ThemeToggle',
            component: DesktopThemeToggleComponent,
            order: 2,
          });
        }
      }, 0);
    };

    // 1) 监听 toolbar 项变化，缺失时补充（依赖 DOM 去重）
    toolbar.items$.subscribe(() => { if (!hasDom()) debouncedAdd(); });

    // 2) 等待布局/顶栏渲染完成后再补一次，避免 APP_INITIALIZER 过早执行
    requestAnimationFrame(() => requestAnimationFrame(() => debouncedAdd()));

    // 3) 路由完成后再校正一次（部分主题在导航后重建顶栏）
    router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => setTimeout(() => debouncedAdd(), 0));
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
