import { NgFor, NgIf } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LocalizationPipe, RoutesService, ABP, TreeNode, eLayoutType } from '@abp/ng.core';
import { LanguageService, LpxLanguage } from '@volo/ngx-lepton-x.core';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-mobile-bottom-nav',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, RouterLinkActive, LocalizationPipe],
  template: `
    <nav class="mobile-bottom-nav d-sm-flex d-md-none" *ngIf="(items() || []).length > 0">
      <a
        *ngFor="let item of items()"
        [routerLink]="item.path"
        routerLinkActive="active"
        [class.has-submenu]="hasSubmenu(item)"
        (click)="onItemClick($event, item)"
        [attr.aria-label]="item.name"
      >
        <i [class]="item.iconClass || 'bi bi-circle'"></i>
        <span>{{ item.name | abpLocalization }}</span>
      </a>
      <!-- 语言按钮打开二级菜单 -->
      <a role="button" (click)="openLanguageMenu($event)" aria-label="language">
        <i class="bi bi-globe"></i>
        <span>{{ 'AbpUi::Language' | abpLocalization }}</span>
      </a>
    </nav>

    <!-- 二级菜单遮罩 -->
    <div class="mobile-submenu-backdrop" *ngIf="submenuOpen" (click)="closeSubmenu()"></div>

    <!-- 二级菜单面板（功能） -->
    <section class="mobile-submenu-panel" *ngIf="submenuOpen && submenuFor !== 'language'">
      <header class="submenu-header">
        <span>{{ submenuTitle | abpLocalization }}</span>
        <button type="button" class="btn-close" aria-label="close" (click)="closeSubmenu()"></button>
      </header>
      <div class="submenu-grid">
        <a *ngFor="let s of submenuItems" [routerLink]="s.path" (click)="closeSubmenu()">
          <i [class]="s.iconClass || 'bi bi-dot' "></i>
          <span>{{ s.name | abpLocalization }}</span>
        </a>
      </div>
    </section>

    <!-- 二级菜单面板（语言） -->
    <section class="mobile-submenu-panel" *ngIf="submenuOpen && submenuFor === 'language'">
      <header class="submenu-header">
        <span>{{ 'AbpUi::Language' | abpLocalization }}</span>
        <button type="button" class="btn-close" aria-label="close" (click)="closeSubmenu()"></button>
      </header>
      <div class="submenu-grid">
        <a *ngFor="let lg of submenuLanguages" role="button" (click)="selectLanguage(lg)">
          <i class="bi bi-translate"></i>
          <span>{{ lg.displayName }}</span>
        </a>
      </div>
    </section>
  `,
  styleUrls: ['./mobile-bottom-nav.component.scss'],
})
export class MobileBottomNavComponent {
  private routes = inject(RoutesService);
  private language = inject(LanguageService);
  submenuOpen = false;
  submenuFor: string | null = null;
  submenuTitle = '';
  submenuItems: ABP.Route[] = [];
  submenuLanguages: LpxLanguage[] = [];

  // 仅取顶层且可见、带 path 的应用路由作为底部导航项
  items = computed<ABP.Route[]>(() => {
    const tree = (this.routes.visible || []) as TreeNode<ABP.Route>[];

    const flatten = (nodes: TreeNode<ABP.Route>[], acc: ABP.Route[] = []): ABP.Route[] => {
      for (const n of nodes) {
        const node = n as unknown as ABP.Route;
        acc.push(node);
        if ((n.children || []).length) flatten(n.children as any, acc);
      }
      return acc;
    };

    const all = flatten(tree).filter(r => !!r.path && (r.layout === eLayoutType.application || !r.layout));
    const wantedPaths = ['/', '/identity', '/tenant-management', '/setting-management', '/books'];
    const pick = new Map<string, ABP.Route>();
    for (const r of all) {
      const path = r.path || '';
      if (wantedPaths.some(p => path === p || path.startsWith(p))) {
        if (!pick.has(path)) pick.set(path, r);
      }
    }
    const order = new Map(wantedPaths.map((p, i) => [p, i] as const));
    return [...pick.values()].sort((a, b) => (order.get(a.path || 'zzz') ?? 99) - (order.get(b.path || 'zzz') ?? 99));
  });

  hasSubmenu(item: ABP.Route): boolean {
    if (!item.path) return false;
    const subs = this.getSubmenuRoutes(item.path);
    return subs.length > 0 && ['/identity', '/tenant-management', '/setting-management'].some(p => item.path === p);
  }

  onItemClick(event: Event, item: ABP.Route) {
    if (this.hasSubmenu(item)) {
      event.preventDefault();
      this.openSubmenu(item);
    }
  }

  openSubmenu(item: ABP.Route) {
    const path = item.path || '';
    this.submenuFor = path;
    this.submenuTitle = item.name || '';
    this.submenuItems = this.getSubmenuRoutes(path);
    this.submenuOpen = true;
  }

  closeSubmenu() {
    this.submenuOpen = false;
    this.submenuFor = null;
    this.submenuItems = [];
    this.submenuLanguages = [];
  }

  private getSubmenuRoutes(rootPath: string): ABP.Route[] {
    const tree = (this.routes.visible || []) as TreeNode<ABP.Route>[];
    const flatten = (nodes: TreeNode<ABP.Route>[], acc: ABP.Route[] = []): ABP.Route[] => {
      for (const n of nodes) {
        const node = n as unknown as ABP.Route;
        acc.push(node);
        if ((n.children || []).length) flatten(n.children as any, acc);
      }
      return acc;
    };
    const all = flatten(tree).filter(r => !!r.path && (r.layout === eLayoutType.application || !r.layout));
    // 取该分组下的后代项（排除自身）
    return all
      .filter(r => (r.path || '').startsWith(rootPath + '/') && (r.path || '') !== rootPath)
      .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  }

  async openLanguageMenu(event: Event) {
    event.preventDefault();
    this.submenuFor = 'language';
    this.submenuTitle = 'AbpUi::Language';
    this.submenuOpen = true;
    try {
      this.submenuLanguages = await firstValueFrom(this.language.languages$);
    } catch {
      this.submenuLanguages = [];
    }
  }

  selectLanguage(lang: LpxLanguage) {
    this.language.setSelectedLanguage(lang);
    this.closeSubmenu();
  }
}
