import { Injectable } from '@angular/core'
import { BehaviorSubject } from 'rxjs'
import { Locale, SUPPORTED_LOCALES, messages } from './messages'

const STORAGE_KEY = 'app.locale'

function resolveInitialLocale(): Locale {
  try {
    const stored = (typeof window !== 'undefined')
      ? (localStorage.getItem(STORAGE_KEY) as Locale | null)
      : null
    if (stored && SUPPORTED_LOCALES.includes(stored)) return stored
  } catch {}

  const nav = (typeof navigator !== 'undefined' && navigator.language) ? navigator.language : 'en'
  const guess: Locale = nav.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en'
  return SUPPORTED_LOCALES.includes(guess) ? guess : 'en'
}

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly localeState = new BehaviorSubject<Locale>(resolveInitialLocale())
  readonly locale$ = this.localeState.asObservable()

  get locale(): Locale {
    return this.localeState.value
  }

  setLocale(l: Locale) {
    if (!SUPPORTED_LOCALES.includes(l)) return
    this.localeState.next(l)
    try {
      if (typeof window !== 'undefined') {
        localStorage.setItem(STORAGE_KEY, l)
      }
    } catch {}
  }

  t(key: string): string {
    const dict = messages[this.locale]
    return (dict && key in dict) ? dict[key] : key
  }
}

