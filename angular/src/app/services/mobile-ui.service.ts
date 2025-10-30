import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

/**
 * 简单的移动端 UI 事件总线：用于在不同组件间打开语言面板等。
 */
@Injectable({ providedIn: 'root' })
export class MobileUiService {
  private _openLanguage$ = new Subject<void>();

  /**
   * 订阅以在其它组件中打开语言面板。
   */
  readonly openLanguage$ = this._openLanguage$.asObservable();

  /** 触发打开语言面板 */
  openLanguage() {
    this._openLanguage$.next();
  }
}

