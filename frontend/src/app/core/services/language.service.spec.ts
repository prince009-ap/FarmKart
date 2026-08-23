import '@angular/compiler';
import { Injector, runInInjectionContext } from '@angular/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of } from 'rxjs';
import { LanguageService } from './language.service';
import { AuthService } from './auth.service';
import { UserPreferenceService } from './user-preference.service';
import { TRANSLATIONS } from '../i18n/translations';

describe('LanguageService', () => {
  let service: LanguageService;
  let authServiceMock: any;
  let preferenceServiceMock: any;

  beforeEach(() => {
    authServiceMock = {
      currentUser$: of(null),
      currentUserValue: null
    };

    preferenceServiceMock = {
      getPreferences: vi.fn().mockReturnValue(of({ language: 'en' })),
      updatePreferences: vi.fn().mockReturnValue(of({ language: 'en' }))
    };

    const injector = Injector.create({
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        { provide: UserPreferenceService, useValue: preferenceServiceMock }
      ]
    });

    service = runInInjectionContext(injector, () => new LanguageService());
  });

  it('should initialize with default English language', () => {
    expect(service.currentLanguage()).toBe('en');
  });

  it('should update language to Hindi and Gujarati', () => {
    service.setLanguage('hi', false);
    expect(service.currentLanguage()).toBe('hi');
    expect(service.t('auth.login')).toBe('लॉग इन करें');

    service.setLanguage('gu', false);
    expect(service.currentLanguage()).toBe('gu');
    expect(service.t('auth.login')).toBe('લોગ ઇન કરો');
  });

  it('should fallback to English if key is missing in Gujarati', () => {
    service.setLanguage('gu', false);
    const result = service.t('nonexistent.key.xyz');
    expect(result).toBe('nonexistent.key.xyz');
  });

  it('should translate status strings correctly', () => {
    service.setLanguage('hi', false);
    expect(service.translateStatus('Active')).toBe('सक्रिय');
    expect(service.translateStatus('Pending')).toBe('लंबित');
  });

  it('should have complete translation keys across en, hi, and gu', () => {
    const enKeys = Object.keys(TRANSLATIONS.en);
    const hiKeys = Object.keys(TRANSLATIONS.hi);
    const guKeys = Object.keys(TRANSLATIONS.gu);

    expect(hiKeys.sort()).toEqual(enKeys.sort());
    expect(guKeys.sort()).toEqual(enKeys.sort());

    for (const section of enKeys) {
      const enSubKeys = Object.keys(TRANSLATIONS.en[section]);
      const hiSubKeys = Object.keys(TRANSLATIONS.hi[section]);
      const guSubKeys = Object.keys(TRANSLATIONS.gu[section]);

      expect(hiSubKeys.sort()).toEqual(enSubKeys.sort());
      expect(guSubKeys.sort()).toEqual(enSubKeys.sort());
    }
  });
});
