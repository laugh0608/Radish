import { Component, ElementRef, HostListener, AfterViewInit, inject } from '@angular/core';
import { LocalizationPipe, ConfigStateService, LocalizationService } from '@abp/ng.core';
import { NgFor, NgIf } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { LanguageService, LpxLanguage } from '@volo/ngx-lepton-x.core';
import { SessionStateService } from '@abp/ng.core';

@Component({
  selector: 'app-mobile-lang-button',
  standalone: true,
  imports: [LocalizationPipe, NgFor, NgIf],
  template: `
    <button
      type="button"
      class="mobile-lang-button d-sm-inline-flex d-md-none"
      [attr.aria-expanded]="open"
      [attr.aria-haspopup]="'menu'"
      [attr.aria-label]="'AbpUi::Language' | abpLocalization"
      (click)="toggle()"
    >
      <i class="bi bi-globe"></i>
    </button>

    <!-- 本地下拉菜单：紧贴悬浮按钮 -->
    <div
      *ngIf="open"
      class="mobile-lang-menu d-sm-block d-md-none"
      [ngStyle]="menuStyle"
      role="menu"
      aria-label="Language menu"
    >
      <button
        *ngFor="let lg of languages()"
        type="button"
        role="menuitemradio"
        class="lang-item"
        [attr.aria-checked]="isActive(lg)"
        (click)="select(lg)"
      >
        <i [class]="isActive(lg) ? 'bi bi-check2-circle' : 'bi bi-translate'"></i>
        <span>{{ lg.displayName }}</span>
      </button>
    </div>
  `,
  styleUrls: ['./mobile-lang-button.component.scss'],
})
export class MobileLangButtonComponent implements AfterViewInit {
  private el = inject(ElementRef<HTMLElement>);
  private language = inject(LanguageService);
  private abpSession = inject(SessionStateService);
  private configState = inject(ConfigStateService);
  private loc = inject(LocalizationService);

  // 菜单开关
  open = false;
  // 计算后的浮层定位样式
  menuStyle: {[k: string]: string} = {};

  // 语言列表与当前语言信号
  languages = toSignal(this.language.languages$, { initialValue: [] as LpxLanguage[] });
  currentLang = toSignal(this.abpSession.getLanguage$(), { initialValue: this.abpSession.getLanguage() });

  toggle() {
    this.open = !this.open;
    if (this.open) {
      this.updateMenuPosition();
    }
  }

  isActive(lg: LpxLanguage): boolean {
    return (this.currentLang() || '').toLowerCase() === (lg.cultureName || '').toLowerCase();
  }

  select(lg: LpxLanguage) {
    this.language.setSelectedLanguage(lg);
    this.abpSession.setLanguage(lg.cultureName);
    try { document.documentElement.lang = lg.cultureName; } catch {}
    try { this.configState.refreshAppState().subscribe(); } catch {}
    this.open = false;
  }

  // 点击外部或按 ESC 关闭
  @HostListener('document:click', ['$event'])
  onDocClick(ev: MouseEvent) {
    if (!this.open) return;
    const root: HTMLElement = this.el.nativeElement;
    if (ev.target instanceof Node && root.contains(ev.target)) return;
    this.open = false;
  }

  @HostListener('document:keydown', ['$event'])
  onKey(ev: KeyboardEvent) {
    if (ev.key === 'Escape') this.open = false;
  }

  ngAfterViewInit(): void {
    // 将本组件节点移动到移动导航的右侧 user-menu 内，实现“固定在顶部栏”
    this.attachToNavbar();
    // 语言切换会触发布局重渲染，延时再次挂载
    this.loc.languageChange$.subscribe(() => setTimeout(() => this.attachToNavbar(), 0));
    // 兜底：窗口尺寸变化后尝试一次
    window.addEventListener('resize', () => setTimeout(() => { this.attachToNavbar(); if (this.open) this.updateMenuPosition(true); }, 0));
    window.addEventListener('scroll', () => { if (this.open) this.updateMenuPosition(true); }, { passive: true });
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

  private updateMenuPosition(force = false) {
    try {
      const host = this.el.nativeElement as HTMLElement;
      const btn = host.querySelector('.mobile-lang-button') as HTMLElement | null;
      if (!btn) return;
      const rect = btn.getBoundingClientRect();
      const margin = 8;
      const top = Math.round(rect.bottom + margin);

      // 先默认靠右对齐
      const preferredRight = Math.max(margin, Math.round(window.innerWidth - rect.right));
      this.menuStyle = { position: 'fixed', top: `${top}px`, right: `${preferredRight}px`, zIndex: '3000' };

      // 下一拍再根据实际宽度校正，避免溢出
      if (!force) {
        setTimeout(() => {
          try {
            const menu = host.querySelector('.mobile-lang-menu') as HTMLElement | null;
            if (!menu) return;
            const mrect = menu.getBoundingClientRect();
            // 如果仍然溢出右侧，则贴右侧边距；否则优先让菜单右边缘与按钮右对齐
            if (mrect.right > window.innerWidth - margin) {
              this.menuStyle = { position: 'fixed', top: `${top}px`, right: `${margin}px`, zIndex: '3000' };
            }
            // 如果左侧溢出，也做保护
            if (mrect.left < margin) {
              this.menuStyle = { position: 'fixed', top: `${top}px`, left: `${margin}px`, zIndex: '3000' } as any;
            }
          } catch {}
        }, 0);
      }
    } catch {}
  }
}
