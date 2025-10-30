import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { MobileLangButtonComponent } from './components/mobile-lang-button/mobile-lang-button.component';
import { MobileThemeToggleComponent } from './components/mobile-theme-toggle/mobile-theme-toggle.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <!-- 移动端右上角：语言与主题切换（仅样式适配，不改变功能） -->
    <app-mobile-lang-button />
    <app-mobile-theme-toggle />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, MobileLangButtonComponent, MobileThemeToggleComponent],
})
export class AppComponent {}
