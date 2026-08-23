import { Injectable, inject, signal } from '@angular/core';
import { SupportedLanguage, TRANSLATIONS, TranslationDictionary } from '../i18n/translations';
import { UserPreferenceService } from './user-preference.service';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private readonly userPreferenceService = inject(UserPreferenceService);
  private readonly authService = inject(AuthService);

  private readonly STORAGE_KEY = 'farmkart_language';

  readonly currentLanguage = signal<SupportedLanguage>(this.getInitialLanguage());

  constructor() {
    // Listen for auth changes to load user preference from backend when logged in
    this.authService.currentUser$.subscribe((user) => {
      if (user) {
        this.syncUserPreferenceFromBackend();
      }
    });
  }

  setLanguage(lang: SupportedLanguage, syncBackend: boolean = true): void {
    if (!['en', 'hi', 'gu'].includes(lang)) {
      lang = 'en';
    }

    this.currentLanguage.set(lang);

    try {
      localStorage.setItem(this.STORAGE_KEY, lang);
    } catch {}

    if (syncBackend && this.authService.currentUserValue) {
      this.userPreferenceService.updatePreferences({
        language: lang,
        theme: 'light',
        emailAlerts: true,
        smsAlerts: true,
        compactView: false
      }).subscribe({
        error: () => {}
      });
    }
  }

  getLanguage(): SupportedLanguage {
    return this.currentLanguage();
  }

  t(key: string, params?: Record<string, any>): string {
    const lang = this.currentLanguage();
    let val = this.resolveKey(TRANSLATIONS[lang], key);

    // Fallback to English if translation is missing in selected language
    if (!val && lang !== 'en') {
      val = this.resolveKey(TRANSLATIONS['en'], key);
    }

    if (!val) {
      val = key;
    }

    if (params && typeof val === 'string') {
      Object.keys(params).forEach((paramKey) => {
        val = (val as string).replace(new RegExp(`{{\\s*${paramKey}\\s*}}`, 'g'), String(params[paramKey]));
      });
    }

    return val;
  }

  translateStatus(status: string): string {
    if (!status) return '';
    const translated = this.t(`status.${status}`);
    return translated !== `status.${status}` ? translated : status;
  }

  private resolveKey(dict: TranslationDictionary, path: string): string | null {
    if (!dict || !path) return null;
    const parts = path.split('.');
    let current: any = dict;

    for (const part of parts) {
      if (current && typeof current === 'object' && part in current) {
        current = current[part];
      } else {
        return null;
      }
    }

    return typeof current === 'string' ? current : null;
  }

  private getInitialLanguage(): SupportedLanguage {
    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (stored && ['en', 'hi', 'gu'].includes(stored)) {
        return stored as SupportedLanguage;
      }
    } catch {}
    return 'en';
  }

  private syncUserPreferenceFromBackend(): void {
    this.userPreferenceService.getPreferences().subscribe({
      next: (pref) => {
        if (pref && pref.language && ['en', 'hi', 'gu'].includes(pref.language.toLowerCase())) {
          this.setLanguage(pref.language.toLowerCase() as SupportedLanguage, false);
        }
      },
      error: () => {}
    });
  }
}
