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
    this.bindPersonIconToProfile();
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

  // 将左起第一个“用户”图标点击行为重定向到个人信息页
  private bindPersonIconToProfile() {
    try {
      const container = document.querySelector('.lpx-mobile-navbar .user-menu') as HTMLElement | null;
      if (!container) return;
      const person = (container.querySelector('lpx-icon[iconclass*="bi-person" i]') as HTMLElement | null)
        || (container.querySelector('lpx-icon:first-child') as HTMLElement | null);
      if (!person || person.getAttribute('data-profile-bound') === '1') return;
      person.setAttribute('data-profile-bound', '1');
      person.style.cursor = 'pointer';
      const handler = (ev: Event) => {
        try { ev.preventDefault(); ev.stopPropagation(); (ev as any).stopImmediatePropagation?.(); } catch {}
        try { document.body.classList.remove('mobile-menu-opened'); document.documentElement.classList.remove('mobile-menu-opened'); } catch {}
        this.router.navigateByUrl('/account/manage');
      };
      person.addEventListener('click', handler, { capture: true });
    } catch {}
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.resizeHandler);
    try { this.media.removeEventListener?.('change', this.mediaHandler as any); } catch {}
    try { this.routeSub.unsubscribe(); } catch {}
  }
}
