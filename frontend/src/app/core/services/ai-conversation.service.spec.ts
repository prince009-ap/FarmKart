import '@angular/compiler';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { AiConversationService } from './ai-conversation.service';
import {
  AiConversationStateResponse,
  SendAiConversationMessageRequest,
  StartAiConversationRequest
} from '../models/ai-conversation.models';
import { environment } from '../../../environments/environment';

describe('AiConversationService', () => {
  let service: AiConversationService;
  let httpClientMock: any;

  beforeEach(() => {
    httpClientMock = {
      post: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: httpClientMock }
      ]
    });

    service = runInInjectionContext(injector, () => new AiConversationService());
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start conversation and set activeSession signal', () => {
    const request: StartAiConversationRequest = {
      taskName: 'test_profile',
      pageName: 'profile',
      language: 'en'
    };

    const mockResponse: AiConversationStateResponse = {
      conversationId: 'session-123',
      taskName: 'test_profile',
      pageName: 'profile',
      language: 'en',
      status: 'Collecting',
      nextQuestion: 'What is your name?',
      currentField: 'name',
      fieldValues: { name: null },
      recentlyExtractedFields: [],
      missingRequiredFields: ['name'],
      missingOptionalFields: []
    };

    httpClientMock.post.mockReturnValue(of(mockResponse));

    service.startConversation(request).subscribe(res => {
      expect(res).toEqual(mockResponse);
    });

    expect(service.activeSession()).toEqual(mockResponse);
    expect(httpClientMock.post).toHaveBeenCalledWith(
      `${environment.apiUrl}/ai/conversation/start`,
      request,
      { withCredentials: true }
    );
  });

  it('should send message and emit field update events on valid extracted field', () => {
    const request: SendAiConversationMessageRequest = {
      conversationId: 'session-123',
      message: 'My name is Prince',
      language: 'en'
    };

    const mockResponse: AiConversationStateResponse = {
      conversationId: 'session-123',
      taskName: 'test_profile',
      pageName: 'profile',
      language: 'en',
      status: 'Collecting',
      nextQuestion: 'What is your phone number?',
      currentField: 'phone',
      fieldValues: { name: 'Prince', phone: null },
      recentlyExtractedFields: [
        { fieldName: 'name', value: 'Prince', isValid: true }
      ],
      missingRequiredFields: ['phone'],
      missingOptionalFields: []
    };

    httpClientMock.post.mockReturnValue(of(mockResponse));

    let emittedEvent: any = null;
    service.fieldUpdated$.subscribe(event => {
      emittedEvent = event;
    });

    service.sendMessage(request).subscribe();

    expect(emittedEvent).toEqual({
      field: 'name',
      value: 'Prince',
      taskName: 'test_profile'
    });
  });

  it('should cancel conversation and clear activeSession signal', () => {
    httpClientMock.post.mockReturnValue(of({ message: 'Cancelled' }));

    service.cancelConversation('session-123').subscribe();

    expect(service.activeSession()).toBeNull();
    expect(httpClientMock.post).toHaveBeenCalledWith(
      `${environment.apiUrl}/ai/conversation/cancel`,
      { conversationId: 'session-123' },
      { withCredentials: true }
    );
  });
});
