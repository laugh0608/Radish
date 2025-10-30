import { AfterViewInit, Component, ElementRef, OnDestroy, inject } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';

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
export class MobileThemeToggleComponent implements AfterViewInit, OnDestroy {
  private el = inject(ElementRef<HTMLElement>);
  private router = inject(Router);

  isDark = false;
  private storageKey = 'data-bs-theme';
  private media = window.matchMedia('(max-width: 767.98px)');
  private resizeHandler = () => setTimeout(() => this.attachToNavbar(), 0);
  private mediaHandler = () => setTimeout(() => this.attachToNavbar(), 0);
  private routeSub = this.router.events.subscribe(e => {
    if (e instanceof NavigationEnd) setTimeout(() => this.attachToNavbar(), 0);
  });

  ngAfterViewInit(): void {
    this.attachToNavbar();
    // 初始主题：localStorage > 文档属性 > 系统偏好
    const doc = document.documentElement;
    const saved = localStorage.getItem(this.storageKey);
    const preferredDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const initTheme = (saved as 'dark' | 'light' | null) || (doc.getAttribute('data-bs-theme') as 'dark' | 'light' | null) || (preferredDark ? 'dark' : 'light');
    this.apply(initTheme);
    // 监听窗口与断点变化，跨视图切换时重新挂载
    window.addEventListener('resize', this.resizeHandler);
    try { this.media.addEventListener?.('change', this.mediaHandler as any); } catch {}
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

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.resizeHandler);
    try { this.media.removeEventListener?.('change', this.mediaHandler as any); } catch {}
    try { this.routeSub.unsubscribe(); } catch {}
  }
}
