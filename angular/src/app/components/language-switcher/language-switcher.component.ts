import { Component, OnDestroy } from '@angular/core'
import { CommonModule } from '@angular/common'
import { I18nPipe } from '../../i18n/i18n.pipe'
import { I18nService } from '../../i18n/i18n.service'
import { Locale, SUPPORTED_LOCALES } from '../../i18n/messages'
import { Subscription } from 'rxjs'

type Option = { value: Locale; labelKey: string }

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule, I18nPipe],
  template: `
    <label class="visually-hidden" [attr.aria-label]="'aria.langSwitcher' | t"></label>
    <select class="form-select form-select-sm w-auto"
            [attr.aria-label]="'aria.langSwitcher' | t"
            [value]="current"
            (change)="onLocaleChange($any($event.target).value)">
      <option *ngFor="let o of options" [value]="o.value">{{ o.labelKey | t }}</option>
    </select>
  `,
})
export default class LanguageSwitcherComponent implements OnDestroy {
  options: Option[] = SUPPORTED_LOCALES.map(l => ({
    value: l,
    labelKey: l === 'zh-CN' ? 'lang.zhCN' : 'lang.en',
  }))

  current: Locale
  private sub: Subscription

  constructor(private readonly i18n: I18nService) {
    this.current = i18n.locale
    this.sub = this.i18n.locale$.subscribe(l => (this.current = l))
  }

  onLocaleChange(value: string) {
    this.i18n.setLocale(value as Locale)
  }

  ngOnDestroy() {
    this.sub?.unsubscribe()
  }
}
