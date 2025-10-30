import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { MobileBottomNavComponent } from './components/mobile-bottom-nav/mobile-bottom-nav.component';
import { MobileLangButtonComponent } from './components/mobile-lang-button/mobile-lang-button.component';
import { MobileThemeToggleComponent } from './components/mobile-theme-toggle/mobile-theme-toggle.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <!-- 移动端底部导航（仅在小屏显示） -->
    <app-mobile-bottom-nav />
    <!-- 移动端顶部右侧语言按钮（覆盖在导航上） -->
    <app-mobile-lang-button />
    <!-- 移动端顶部右侧主题按钮 -->
    <app-mobile-theme-toggle />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, MobileBottomNavComponent, MobileLangButtonComponent, MobileThemeToggleComponent],
})
export class AppComponent {}
