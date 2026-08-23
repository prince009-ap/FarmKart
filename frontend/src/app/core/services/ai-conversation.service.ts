import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AiConversationStateResponse,
  AiFieldUpdatedEvent,
  CancelAiConversationRequest,
  SendAiConversationMessageRequest,
  StartAiConversationRequest
} from '../models/ai-conversation.models';

@Injectable({
  providedIn: 'root'
})
export class AiConversationService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/ai/conversation`;

  readonly activeSession = signal<AiConversationStateResponse | null>(null);

  readonly sessionStarted$ = new Subject<AiConversationStateResponse>();
  readonly fieldUpdated$ = new Subject<AiFieldUpdatedEvent>();
  readonly formCompleted$ = new Subject<{ taskName: string; data: Record<string, string | null> }>();
  readonly formCancelled$ = new Subject<void>();

  startConversation(request: StartAiConversationRequest): Observable<AiConversationStateResponse> {
    return this.http.post<AiConversationStateResponse>(`${this.apiUrl}/start`, request, { withCredentials: true }).pipe(
      tap(res => {
        this.activeSession.set(res);
        this.emitFieldUpdates(res);
        this.sessionStarted$.next(res);
      })
    );
  }

  sendMessage(request: SendAiConversationMessageRequest): Observable<AiConversationStateResponse> {
    return this.http.post<AiConversationStateResponse>(`${this.apiUrl}/message`, request, { withCredentials: true }).pipe(
      tap(res => {
        this.activeSession.set(res);
        this.emitFieldUpdates(res);
        if (res.status === 'Cancelled') {
          this.formCancelled$.next();
          this.activeSession.set(null);
        }
      })
    );
  }

  cancelConversation(conversationId: string): Observable<void> {
    const request: CancelAiConversationRequest = { conversationId };
    return this.http.post<void>(`${this.apiUrl}/cancel`, request, { withCredentials: true }).pipe(
      tap(() => {
        this.activeSession.set(null);
        this.formCancelled$.next();
      })
    );
  }

  confirmAndComplete(): void {
    const session = this.activeSession();
    if (session) {
      this.formCompleted$.next({
        taskName: session.taskName,
        data: { ...session.fieldValues }
      });
      this.activeSession.set(null);
    }
  }

  private emitFieldUpdates(res: AiConversationStateResponse): void {
    if (res.recentlyExtractedFields && res.recentlyExtractedFields.length > 0) {
      for (const extracted of res.recentlyExtractedFields) {
        if (extracted.isValid) {
          this.fieldUpdated$.next({
            field: extracted.fieldName,
            value: extracted.value,
            taskName: res.taskName
          });
        }
      }
    }
  }
}
