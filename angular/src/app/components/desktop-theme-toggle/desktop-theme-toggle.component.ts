import { AfterViewInit, Component } from '@angular/core';

@Component({
  selector: 'app-desktop-theme-toggle',
  standalone: true,
  template: `
    <button
      type="button"
      class="desktop-icon-btn d-none d-md-inline-flex"
      [attr.aria-label]="isDark ? 'Switch to light' : 'Switch to dark'"
      (click)="toggle()"
      title="Toggle theme"
    >
      <i [class]="isDark ? 'bi bi-sun' : 'bi bi-moon'"></i>
    </button>
  `,
})
export class DesktopThemeToggleComponent implements AfterViewInit {
  isDark = false;
  private storageKey = 'data-bs-theme';

  ngAfterViewInit(): void {
    const doc = document.documentElement;
    const saved = localStorage.getItem(this.storageKey) as 'dark' | 'light' | null;
    const preferredDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const initTheme = saved || (doc.getAttribute('data-bs-theme') as 'dark' | 'light' | null) || (preferredDark ? 'dark' : 'light');
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
}
