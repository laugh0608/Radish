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
        [attr.aria-label]="item.name"
      >
        <i [class]="item.iconClass || 'bi bi-circle'"></i>
        <span>{{ item.name | abpLocalization }}</span>
      </a>
      <!-- 语言切换：点击在已配置语言间循环 -->
      <a role="button" (click)="switchLanguage($event)" aria-label="language">
        <i class="bi bi-globe"></i>
        <span>{{ 'AbpUi::Language' | abpLocalization }}</span>
      </a>
    </nav>
  `,
  styleUrls: ['./mobile-bottom-nav.component.scss'],
})
export class MobileBottomNavComponent {
  private routes = inject(RoutesService);
  private language = inject(LanguageService);

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

  async switchLanguage(event: Event) {
    event.preventDefault();
    try {
      const langs = await firstValueFrom(this.language.languages$);
      if (!langs || langs.length === 0) return;
      const current = this.language.selectedLanguage;
      const idx = Math.max(0, langs.findIndex(l => l.cultureName === current?.cultureName));
      const next = langs[(idx + 1) % langs.length] as LpxLanguage;
      this.language.setSelectedLanguage(next);
    } catch {
      // ignore
    }
  }
}
