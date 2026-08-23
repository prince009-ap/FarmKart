import '@angular/compiler';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of, throwError } from 'rxjs';
import { AiService } from './ai.service';
import { AiChatRequest, AiChatResponse } from '../models/ai.models';
import { environment } from '../../../environments/environment';

describe('AiService', () => {
  let service: AiService;
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
    service = runInInjectionContext(injector, () => new AiService());
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST request to /api/ai/chat and return AI response', () => {
    const mockRequest: AiChatRequest = {
      message: 'Hello',
      language: 'en'
    };
    const mockResponse: AiChatResponse = {
      message: 'Hello! How can I help you?',
      language: 'en'
    };

    httpClientMock.post.mockReturnValue(of(mockResponse));

    service.chat(mockRequest).subscribe(res => {
      expect(res).toEqual(mockResponse);
    });

    expect(httpClientMock.post).toHaveBeenCalledWith(
      `${environment.apiUrl}/ai/chat`,
      mockRequest,
      { withCredentials: true }
    );
  });

  it('should propagate HTTP error on chat failure', () => {
    const mockRequest: AiChatRequest = {
      message: 'Hello',
      language: 'en'
    };

    httpClientMock.post.mockReturnValue(throwError(() => new Error('Service Unavailable')));

    service.chat(mockRequest).subscribe({
      error: (err) => {
        expect(err.message).toBe('Service Unavailable');
      }
    });
  });
});
