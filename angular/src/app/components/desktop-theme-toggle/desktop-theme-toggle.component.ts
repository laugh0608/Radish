import { AfterViewInit, Component } from '@angular/core';

@Component({
  selector: 'app-desktop-theme-toggle',
  standalone: true,
  template: `
    <button
      type="button"
      class="btn btn-link p-0 d-none d-md-inline-flex align-items-center"
      [attr.aria-label]="isDark ? 'Switch to light' : 'Switch to dark'"
      (click)="toggle()"
      title="Toggle theme"
    >
      <i [class]="isDark ? 'bi bi-sun fs-5' : 'bi bi-moon fs-5'"></i>
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

