import { AfterViewInit, Component, ElementRef, HostListener, inject } from '@angular/core';

@Component({
  selector: 'app-mobile-theme-toggle',
  standalone: true,
  template: `
    <button
      type="button"
      class="mobile-theme-btn d-sm-inline-flex d-md-none"
      [attr.aria-label]="isDark ? 'Switch to light' : 'Switch to dark'"
      (click)="toggle()"
    >
      <i [class]="isDark ? 'bi bi-sun' : 'bi bi-moon' "></i>
    </button>
  `,
  styleUrls: ['./mobile-theme-toggle.component.scss'],
})
export class MobileThemeToggleComponent implements AfterViewInit {
  private el = inject(ElementRef<HTMLElement>);

  isDark = false;
  private storageKey = 'data-bs-theme';

  ngAfterViewInit(): void {
    this.attachToNavbar();
    // 初始主题：localStorage > 文档属性 > 系统偏好
    const doc = document.documentElement;
    const saved = localStorage.getItem(this.storageKey);
    const preferredDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const initTheme = (saved as 'dark' | 'light' | null) || (doc.getAttribute('data-bs-theme') as 'dark' | 'light' | null) || (preferredDark ? 'dark' : 'light');
    this.apply(initTheme);
  }

  toggle() {
    this.apply(this.isDark ? 'light' : 'dark');
  }

  private apply(theme: 'light' | 'dark') {
    this.isDark = theme === 'dark';
    try {
      document.documentElement.setAttribute('data-bs-theme', theme);
      localStorage.setItem(this.storageKey, theme);
    } catch {}
  }

  private attachToNavbar() {
    try {
      const host: HTMLElement = this.el.nativeElement as HTMLElement;
      const target = document.querySelector('.lpx-mobile-navbar .user-menu') as HTMLElement | null;
      if (target && host.parentElement !== target) {
        target.appendChild(host);
      }
    } catch {}
  }
}

