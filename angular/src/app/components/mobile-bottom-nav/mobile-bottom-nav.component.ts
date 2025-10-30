import { NgFor, NgIf } from '@angular/common';
import { Component, HostListener, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, NavigationEnd } from '@angular/router';
import { LocalizationPipe, RoutesService, ABP, TreeNode, eLayoutType, SessionStateService, ConfigStateService } from '@abp/ng.core';
import { LanguageService, LpxLanguage } from '@volo/ngx-lepton-x.core';
import { firstValueFrom } from 'rxjs';
import { MobileUiService } from '../../services/mobile-ui.service';

@Component({
  selector: 'app-mobile-bottom-nav',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, RouterLinkActive, LocalizationPipe],
  template: `
    <nav class="mobile-bottom-nav d-sm-flex d-md-none" [class.hidden]="navHidden" *ngIf="(items() || []).length > 0">
      <ng-container *ngFor="let item of items()">
        @if (hasSubmenu(item)) {
          <a role="button" class="has-submenu" (click)="openSubmenu(item)" [attr.aria-label]="item.name">
            <i [class]="iconFor(item)"></i>
            <span>{{ item.name | abpLocalization }}</span>
          </a>
        } @else {
          <a [routerLink]="item.path" routerLinkActive="active" [attr.aria-label]="item.name">
            <i [class]="iconFor(item)"></i>
            <span>{{ item.name | abpLocalization }}</span>
          </a>
        }
      </ng-container>
      <!-- 更多（收纳溢出项） -->
      <a role="button" *ngIf="(overflow() || []).length > 0" (click)="openMore($event)">
        <i class="bi bi-grid-3x3-gap-fill"></i>
        <span>更多</span>
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
        <a *ngFor="let s of submenuItems" role="button" (click)="go(s)">
          <i [class]="iconFor(s)"></i>
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
          <i [class]="lg.cultureName === language.selectedLanguage?.cultureName ? 'bi bi-check2-circle' : 'bi bi-translate'"></i>
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
  private abpSession = inject(SessionStateService);
  private router = inject(Router);
  private mobileUi = inject(MobileUiService);
  private configState = inject(ConfigStateService);
  submenuOpen = false;
  submenuFor: string | null = null;
  submenuTitle = '';
  submenuItems: ABP.Route[] = [];
  submenuLanguages: LpxLanguage[] = [];
  navHidden = false;
  private lastScrollY = 0;
  // 监听语言变化以驱动重算
  private currentLang = toSignal(this.abpSession.getLanguage$(), { initialValue: this.abpSession.getLanguage() });
  // 路由可见树的信号：当后台资源随语言刷新后，自动重新计算菜单
  private visibleRoutes = toSignal(this.routes.visible$, { initialValue: this.routes.visible });

  // 仅取顶层且可见、带 path 的应用路由作为底部导航项
  // 顶部可见的所有顶层路由（与 PC 一致）
  private topRoutes = computed<ABP.Route[]>(() => {
    // 读取语言信号以在切换语言后重算名称
    this.currentLang();
    // 依赖 visibleRoutes() 以便在刷新 ApplicationConfiguration 后立刻生效
    const tree = (this.visibleRoutes() || []) as TreeNode<ABP.Route>[];
    const list: ABP.Route[] = [];
    for (const n of tree) {
      const isTop = !n.parent && (n.layout === eLayoutType.application || !n.layout);
      if (!isTop) continue;
      const node = n as any;
      if (node.path) {
        list.push(node as ABP.Route);
      } else {
        // 顶层分组（无 path），转成伪路由，点击打开子菜单
        const name: string = node.name || 'Group';
        const pseudo: ABP.Route = {
          path: `/__group__/${encodeURIComponent(name)}`,
          name,
          iconClass: this.iconForGroup(name),
          order: node.order,
        } as ABP.Route;
        list.push(pseudo);
      }
    }
    return list.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  });

  // 底部栏主导航项（其余的放到“更多”）
  private maxPrimary = 4;
  private adminPaths = ['/identity', '/tenant-management', '/setting-management'];
  private homePath = '/';

  private withManageGroup = computed<ABP.Route[]>(() => {
    const roots = this.topRoutes();
    const admins = roots.filter(r => this.adminPaths.includes(r.path || ''));
    if (admins.length === 0) return roots;

    // 如果已有顶层分组（如 Administration），将其固定到首页之后
    const adminGroup = roots.find(r => (r.path || '').startsWith('/__group__/') && this.isAdminGroupName(decodeURIComponent((r.path || '').replace('/__group__/', ''))));
    if (adminGroup) {
      const home = roots.find(r => (r.path || '') === this.homePath);
      const rest = roots.filter(r => r !== home && r !== adminGroup);
      const result: ABP.Route[] = [];
      if (home) result.push(home);
      result.push(adminGroup);
      result.push(...rest);
      return result;
    }

    const manage: ABP.Route = {
      path: '/__manage',
      name: this.isZh() ? '管理' : 'Administration',
      iconClass: 'bi bi-tools',
      order: (roots.find(r => (r.path || '') === this.homePath)?.order ?? 0) + 1,
    } as ABP.Route;

    const result: ABP.Route[] = [];
    // 保留 Home 在前
    const home = roots.find(r => (r.path || '') === this.homePath);
    if (home) result.push(home);
    // 插入管理
    result.push(manage);
    // 其余（去掉三个管理模块）保持原顺序
    for (const r of roots) {
      const p = r.path || '';
      if (p === this.homePath) continue;
      if (this.adminPaths.includes(p)) continue;
      result.push(r);
    }
    return result;
  });

  items = computed<ABP.Route[]>(() => this.withManageGroup().slice(0, this.maxPrimary));
  overflow = computed<ABP.Route[]>(() => this.withManageGroup().slice(this.maxPrimary));

  hasSubmenu(item: ABP.Route): boolean {
    if (!item.path) return false;
    if (item.path === '/__manage') return true;
    if (item.path.startsWith('/__group__/')) return true;
    const subs = this.getSubmenuRoutes(item.path);
    return subs.length > 0;
  }

  onItemClick(event: Event, item: ABP.Route) {
    if (this.hasSubmenu(item)) {
      event.preventDefault();
      (event as any).stopImmediatePropagation?.();
      event.stopPropagation();
      this.openSubmenu(item);
    }
  }

  openSubmenu(item: ABP.Route) {
    const path = item.path || '';
    this.submenuFor = path;
    this.submenuTitle = item.name || '';
    if (path === '/__manage') {
      // 聚合“身份/租户/设置”作为二级菜单
      const roots = this.topRoutes();
      this.submenuItems = roots.filter(r => this.adminPaths.includes(r.path || ''));
    } else if (path.startsWith('/__group__/')) {
      const groupName = decodeURIComponent(path.replace('/__group__/', ''));
      this.submenuItems = this.getGroupChildren(groupName);
    } else {
      this.submenuItems = this.getSubmenuRoutes(path);
    }
    this.submenuOpen = true;
    this.navHidden = false;
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

  private getGroupChildren(groupName: string): ABP.Route[] {
    const tree = (this.routes.visible || []) as TreeNode<ABP.Route>[];
    const group = tree.find(n => !n.parent && (n as any).name === groupName);
    if (!group) return [];
    const collect = (nodes: TreeNode<ABP.Route>[], acc: ABP.Route[] = []): ABP.Route[] => {
      for (const n of nodes) {
        const v = n as any as ABP.Route;
        const isApp = (v.layout === eLayoutType.application || !v.layout);
        if (!isApp) continue;
        if (v.path) {
          acc.push(v);
        }
        if ((n.children || []).length) collect(n.children as any, acc);
      }
      return acc;
    };
    return collect(group.children || [])
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
    this.abpSession.setLanguage(lang.cultureName);
    try { document.documentElement.lang = lang.cultureName; } catch {}
    // 立即刷新 App 配置（菜单/本地化资源），避免需手动刷新
    try { this.configState.refreshAppState().subscribe(); } catch {}
    this.closeSubmenu();
  }

  go(item: ABP.Route) {
    if (!item || !item.path) return;
    if (item.path === '/__language') {
      // 打开语言面板
      this.openLanguageMenu(new MouseEvent('click'));
      return;
    }
    this.router.navigateByUrl(item.path);
    this.closeSubmenu();
  }

  // ----- UI helpers -----
  iconFor(r: ABP.Route | undefined): string {
    if (!r) return 'bi bi-dot';
    const path = r.path || '';
    const map: Record<string, string> = {
      '/': 'bi bi-house-fill',
      '/books': 'bi bi-journal-bookmark-fill',
      '/__manage': 'bi bi-tools',
      '/__language': 'bi bi-globe',
      '/identity': 'bi bi-people-fill',
      '/identity/users': 'bi bi-person-fill',
      '/identity/roles': 'bi bi-person-badge-fill',
      '/tenant-management': 'bi bi-buildings-fill',
      '/tenant-management/tenants': 'bi bi-building-fill',
      '/setting-management': 'bi bi-gear-fill',
    };
    // longest prefix match
    const matched = Object.keys(map)
      .sort((a, b) => b.length - a.length)
      .find(p => path === p || path.startsWith(p + '/'));
    return r.iconClass || (matched ? map[matched] : 'bi bi-dot');
  }

  private iconForGroup(name: string): string {
    const key = (name || '').toLowerCase();
    if (key.includes('管理') || key.includes('administration') || key.includes('manage')) return 'bi bi-tools';
    return 'bi bi-grid';
  }

  private isAdminGroupName(name: string): boolean {
    const key = (name || '').toLowerCase();
    return key.includes('管理') || key.includes('administration') || key.includes('manage');
  }

  private isZh(): boolean {
    const l = (this.currentLang() || '').toLowerCase();
    return l.startsWith('zh');
  }

  // 关闭弹层：路由变化时
  constructor() {
    this.router.events.subscribe(e => {
      if (e instanceof NavigationEnd) this.closeSubmenu();
    });
    // 监听“顶部语言按钮”的打开请求
    this.mobileUi.openLanguage$.subscribe(() => {
      this.openLanguageMenu(new MouseEvent('click'));
      this.navHidden = false;
    });

    // 语言变化时，如二级面板已打开，实时刷新面板内容
    effect(() => {
      this.currentLang();
      this.visibleRoutes();
      if (!this.submenuOpen) return;
      if (this.submenuFor === 'language') {
        // 语言面板：刷新候选列表
        firstValueFrom(this.language.languages$).then(l => (this.submenuLanguages = l)).catch(() => (this.submenuLanguages = []));
        return;
      }
      if (this.submenuFor === 'more') {
        // 重新打开“更多”以重算语言项文案
        this.openMore(new MouseEvent('click'));
        return;
      }
      // 普通功能分组/路由：基于当前路由树重算
      const key = this.submenuFor || '';
      if (key.startsWith('/__group__')) {
        const name = decodeURIComponent(key.replace('/__group__/', ''));
        this.submenuItems = this.getGroupChildren(name);
      } else if (key === '/__manage') {
        const roots = this.topRoutes();
        this.submenuItems = roots.filter(r => this.adminPaths.includes(r.path || ''));
      } else {
        this.submenuItems = this.getSubmenuRoutes(key);
      }
    });
  }

  // 滚动隐藏 / 上滑显示
  @HostListener('window:scroll', [])
  onScroll() {
    const y = window.scrollY || 0;
    const diff = y - this.lastScrollY;
    if (Math.abs(diff) > 5) {
      this.navHidden = diff > 0; // 下滑隐藏，上滑显示
      this.lastScrollY = y;
    }
    // 停止滚动后自动显示
    clearTimeout(this._scrollTimer);
    this._scrollTimer = setTimeout(() => {
      this.navHidden = false;
    }, 250);
  }

  openMore(event: Event) {
    event.preventDefault();
    this.submenuFor = 'more';
    this.submenuTitle = '更多';
    const items = [...this.overflow()];
    const langItem: ABP.Route = {
      path: '/__language',
      name: this.isZh() ? '语言' : 'Language',
      iconClass: 'bi bi-globe',
      order: 999,
    } as ABP.Route;
    items.push(langItem);
    this.submenuItems = items;
    this.submenuOpen = true;
    this.navHidden = false;
  }

  private _scrollTimer: any;
}
