import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { MobileBottomNavComponent } from './components/mobile-bottom-nav/mobile-bottom-nav.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <!-- 移动端底部导航（仅在小屏显示） -->
    <app-mobile-bottom-nav />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, MobileBottomNavComponent],
})
export class AppComponent {}
