import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { MobileLangButtonComponent } from './components/mobile-lang-button/mobile-lang-button.component';
import { MobileThemeToggleComponent } from './components/mobile-theme-toggle/mobile-theme-toggle.component';
import { MobileBottomNavComponent } from './components/mobile-bottom-nav/mobile-bottom-nav.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <!-- PC 顶栏顺序通过 CSS 控制（styles.scss），无需运行时重排 -->
    <!-- 移动端右上角：语言与主题切换（仅样式适配，不改变功能） -->
    <app-mobile-lang-button />
    <app-mobile-theme-toggle />
    <!-- 移动端底部导航：主要功能在主栏，次要功能收纳在二级菜单/更多面板 -->
    <app-mobile-bottom-nav />
  `,
  standalone: true,
  imports: [LoaderBarComponent, DynamicLayoutComponent, MobileLangButtonComponent, MobileThemeToggleComponent, MobileBottomNavComponent],
})
export class AppComponent {}
