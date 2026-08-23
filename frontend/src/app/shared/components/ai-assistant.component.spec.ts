import '@angular/compiler';
import { Injector, runInInjectionContext } from '@angular/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Subject, of, throwError } from 'rxjs';
import { AiAssistantComponent } from './ai-assistant.component';
import { AuthService } from '../../core/services/auth.service';
import { AiService } from '../../core/services/ai.service';
import { AiConversationService } from '../../core/services/ai-conversation.service';
import { UserPreferenceService } from '../../core/services/user-preference.service';

describe('AiAssistantComponent', () => {
  let component: AiAssistantComponent;
  let aiServiceMock: any;
  let conversationServiceMock: any;
  let authServiceMock: any;
  let preferenceServiceMock: any;
  let sessionStartedSubject: Subject<any>;

  beforeEach(() => {
    sessionStartedSubject = new Subject<any>();

    aiServiceMock = {
      chat: vi.fn().mockReturnValue(of({ message: 'Hello response', language: 'en' }))
    };

    conversationServiceMock = {
      activeSession: vi.fn().mockReturnValue(null),
      sessionStarted$: sessionStartedSubject,
      startConversation: vi.fn().mockReturnValue(of({
        conversationId: 'session-123',
        taskName: 'create_farmer_crop',
        nextQuestion: 'What crop would you like to add?'
      })),
      sendMessage: vi.fn().mockReturnValue(of({ nextQuestion: 'Next task question?' })),
      cancelConversation: vi.fn().mockReturnValue(of(void 0)),
      confirmAndComplete: vi.fn()
    };

    authServiceMock = {
      currentUser$: of({ userId: 'user-1', email: 'test@example.com', fullName: 'Test User', role: 'Customer' })
    };

    preferenceServiceMock = {
      getPreferences: vi.fn().mockReturnValue(of({ language: 'en' }))
    };

    const injector = Injector.create({
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        { provide: AiService, useValue: aiServiceMock },
        { provide: AiConversationService, useValue: conversationServiceMock },
        { provide: UserPreferenceService, useValue: preferenceServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new AiAssistantComponent());
  });

  it('should create AI assistant component', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with closed panel and default English language', () => {
    expect(component.isOpen()).toBe(false);
    expect(component.selectedLanguage()).toBe('en');
  });

  it('should open and close panel on toggle', () => {
    expect(component.isOpen()).toBe(false);

    component.togglePanel();
    expect(component.isOpen()).toBe(true);

    component.togglePanel();
    expect(component.isOpen()).toBe(false);
  });

  it('should update language on selection', () => {
    component.onLanguageChange('hi');
    expect(component.selectedLanguage()).toBe('hi');

    component.onLanguageChange('gu');
    expect(component.selectedLanguage()).toBe('gu');
  });

  it('should auto-open panel and render task question when sessionStarted$ fires', () => {
    component.ngOnInit();
    sessionStartedSubject.next({
      conversationId: 'session-123',
      taskName: 'create_farmer_crop',
      pageName: 'crop',
      language: 'en',
      status: 'Collecting',
      nextQuestion: 'What crop would you like to add?'
    });

    expect(component.isOpen()).toBe(true);
    const lastMsg = component.messages()[component.messages().length - 1];
    expect(lastMsg.text).toContain('Crop Assistant (Add Crop)');
    expect(lastMsg.text).toContain('What crop would you like to add?');
  });

  it('should send user message and append AI response in freeform mode', () => {
    component.isOpen.set(true);
    component.inputText = 'I need help with my crops';

    component.sendMessage();

    expect(aiServiceMock.chat).toHaveBeenCalledWith(expect.objectContaining({
      message: 'I need help with my crops',
      language: 'en'
    }));

    expect(component.messages().length).toBeGreaterThan(1);
    const lastMsg = component.messages()[component.messages().length - 1];
    expect(lastMsg.sender).toBe('ai');
    expect(lastMsg.text).toBe('Hello response');
  });

  it('should handle API error gracefully', () => {
    aiServiceMock.chat.mockReturnValue(throwError(() => ({ message: 'AI unavailable' })));
    component.isOpen.set(true);
    component.inputText = 'Help me';

    component.sendMessage();

    expect(component.statusMessage()).toBeTruthy();
  });

  it('should handle unsupported voice browser gracefully', () => {
    component.toggleListening();
    expect(component.statusMessage()).toContain('not supported');
  });
});
