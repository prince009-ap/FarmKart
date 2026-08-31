import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, OnDestroy, inject, signal, ElementRef, ViewChild, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { AiService } from '../../core/services/ai.service';
import { AiConversationService } from '../../core/services/ai-conversation.service';
import { AiChatMessageDto, AiLanguage, AiMessageItem } from '../../core/models/ai.models';
import { StartAiConversationRequest } from '../../core/models/ai-conversation.models';
import { UserPreferenceService } from '../../core/services/user-preference.service';
import { LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-ai-assistant',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    @if (authService.currentUser$ | async) {
      <!-- Floating Trigger Button -->
      <button
        type="button"
        (click)="togglePanel()"
        class="fixed bottom-6 right-6 z-50 w-14 h-14 rounded-full bg-gradient-to-r from-emerald-600 to-teal-600 text-white shadow-2xl hover:scale-105 transition-all duration-300 flex items-center justify-center border-2 border-emerald-300/40 group focus:outline-hidden"
        [matTooltip]="isOpen() ? 'Close FarmKart AI' : 'FarmKart AI Assistant'"
        aria-label="FarmKart AI Assistant">
        <mat-icon class="!w-7 !h-7 !text-[28px] transition-transform duration-300 group-hover:rotate-12">
          {{ isOpen() ? 'close' : 'smart_toy' }}
        </mat-icon>
        @if (!isOpen()) {
          <span class="absolute -top-1 -right-1 flex h-4 w-4">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-4 w-4 bg-amber-400 border border-slate-900"></span>
          </span>
        }
      </button>

      <!-- Chat Panel Window -->
      @if (isOpen()) {
        <div class="fixed bottom-24 right-4 sm:right-6 z-50 w-[calc(100vw-2rem)] sm:w-96 h-[540px] max-h-[calc(100vh-8rem)] bg-slate-900 text-slate-100 rounded-2xl shadow-2xl border border-slate-800 flex flex-col overflow-hidden animate-fk-rise">
          <!-- Header -->
          <div class="px-4 py-3.5 bg-gradient-to-r from-slate-900 via-emerald-950/80 to-slate-900 border-b border-slate-800 flex items-center justify-between gap-2">
            <div class="flex items-center gap-2.5">
              <div class="w-8 h-8 rounded-xl bg-emerald-600/30 border border-emerald-500/40 text-emerald-400 flex items-center justify-center">
                <mat-icon class="!w-5 !h-5 !text-[20px]">smart_toy</mat-icon>
              </div>
              <div>
                <h3 class="font-bold text-sm text-white flex items-center gap-1.5 leading-tight">
                  FarmKart AI
                  <span class="text-[10px] font-semibold uppercase px-1.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                    {{ taskSession() ? 'Form Engine' : 'Assistant' }}
                  </span>
                </h3>
                <p class="text-[11px] text-slate-400 leading-none mt-0.5">
                  {{ taskSession() ? getTaskTitle(taskSession()?.taskName || '') : 'Multilingual Voice Assistant' }}
                </p>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <!-- Language Selector -->
              <select
                [ngModel]="selectedLanguage()"
                (ngModelChange)="onLanguageChange($event)"
                class="bg-slate-800 text-slate-200 text-xs rounded-lg px-2 py-1 border border-slate-700 focus:outline-hidden focus:border-emerald-500 cursor-pointer">
                <option value="en">English</option>
                <option value="hi">हिंदी</option>
                <option value="gu">ગુજરાતી</option>
              </select>

              <button
                type="button"
                (click)="togglePanel()"
                class="w-7 h-7 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 flex items-center justify-center transition-colors">
                <mat-icon class="!w-4 !h-4 !text-[18px]">close</mat-icon>
              </button>
            </div>
          </div>

          <!-- Active Contextual Task Bar -->
          @if (taskSession()) {
            <div class="px-3 py-1.5 bg-slate-950/80 border-b border-slate-800 flex items-center justify-between">
              <div class="flex items-center justify-between w-full text-xs">
                <span class="text-emerald-400 font-medium flex items-center gap-1">
                  <mat-icon class="!w-4 !h-4 !text-[16px]">assignment</mat-icon>
                  {{ getTaskTitle(taskSession()?.taskName || '') }}
                </span>
                <button
                  type="button"
                  (click)="cancelTaskSession()"
                  class="text-rose-400 hover:text-rose-300 hover:underline font-semibold text-[11px]">
                  Cancel Task
                </button>
              </div>
            </div>
          }

          <!-- Message History Window -->
          <div #scrollContainer class="flex-1 p-4 overflow-y-auto space-y-3.5 bg-slate-950/60">
            @for (msg of messages(); track msg.id) {
              <div class="flex items-start gap-2.5" [class.flex-row-reverse]="msg.sender === 'user'">
                <!-- Avatar -->
                <div class="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold shrink-0 shadow-sm"
                     [ngClass]="msg.sender === 'user' ? 'bg-sky-600 text-white' : 'bg-emerald-600/30 text-emerald-400 border border-emerald-500/30'">
                  <mat-icon class="!w-4 !h-4 !text-[16px]">
                    {{ msg.sender === 'user' ? 'person' : 'smart_toy' }}
                  </mat-icon>
                </div>

                <!-- Message Bubble -->
                <div class="max-w-[85%] rounded-2xl px-3.5 py-2.5 text-xs leading-relaxed shadow-sm"
                     [ngClass]="msg.sender === 'user' 
                       ? 'bg-sky-600 text-white rounded-tr-none' 
                       : 'bg-slate-900 border border-slate-800 text-slate-200 rounded-tl-none'">
                  <p class="whitespace-pre-wrap font-sans">{{ msg.text }}</p>
                  <span class="block text-[9px] mt-1 text-slate-400 text-right">
                    {{ msg.timestamp | date:'shortTime' }}
                  </span>
                </div>
              </div>
            }

            <!-- Ready For Confirmation Card -->
            @if (taskSession()?.status === 'ReadyForConfirmation') {
              <div class="bg-emerald-950/40 border border-emerald-600/40 rounded-xl p-3.5 space-y-3 text-xs shadow-lg animate-fk-rise">
                <div class="flex items-center gap-2 text-emerald-400 font-semibold border-b border-emerald-800/50 pb-2">
                  <mat-icon class="!w-4 !h-4 !text-[18px]">fact_check</mat-icon>
                  <span>Confirmation Summary</span>
                </div>
                <p class="whitespace-pre-wrap text-slate-200 font-mono text-[11px] bg-slate-950/80 p-2.5 rounded-lg border border-slate-800">
                  {{ taskSession()?.summaryText }}
                </p>
                <div class="flex items-center gap-2 pt-1">
                  <button
                    type="button"
                    (click)="confirmTaskDetails()"
                    class="flex-1 bg-emerald-600 hover:bg-emerald-500 text-white py-1.5 px-3 rounded-lg font-medium text-xs flex items-center justify-center gap-1 transition-colors">
                    <mat-icon class="!w-4 !h-4 !text-[16px]">check_circle</mat-icon>
                    Confirm & Save
                  </button>
                  <button
                    type="button"
                    (click)="cancelTaskSession()"
                    class="bg-slate-800 hover:bg-slate-700 text-slate-300 py-1.5 px-3 rounded-lg text-xs transition-colors">
                    Cancel
                  </button>
                </div>
              </div>
            }

            <!-- Thinking Indicator -->
            @if (loading()) {
              <div class="flex items-center gap-2 text-xs text-emerald-400 bg-emerald-950/30 border border-emerald-800/40 p-2.5 rounded-xl w-fit animate-pulse">
                <mat-icon class="!w-4 !h-4 !text-[16px] animate-spin">sync</mat-icon>
                <span>AI is thinking...</span>
              </div>
            }

            <!-- System Info Warning / Error Banner -->
            @if (statusMessage()) {
              <div class="bg-amber-950/50 border border-amber-800/60 text-amber-300 text-[11px] p-2.5 rounded-xl flex items-center gap-2">
                <mat-icon class="!w-4 !h-4 !text-[16px] text-amber-400 shrink-0">info</mat-icon>
                <span class="flex-1">{{ statusMessage() }}</span>
                <button type="button" (click)="statusMessage.set(null)" class="text-amber-400 hover:text-amber-200">
                  <mat-icon class="!w-3.5 !h-3.5 !text-[14px]">close</mat-icon>
                </button>
              </div>
            }
          </div>

          <!-- Input Footer Area -->
          <div class="p-3 bg-slate-900 border-t border-slate-800 space-y-2">
            @if (isListening()) {
              <div class="flex items-center justify-between text-xs text-emerald-400 px-1 animate-pulse">
                <span class="flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
                  Listening ({{ getLanguageLabel(selectedLanguage()) }})...
                </span>
                <button type="button" (click)="stopListening()" class="text-xs text-rose-400 hover:underline font-semibold">
                  Stop
                </button>
              </div>
            }

            <form (ngSubmit)="sendMessage()" class="flex items-center gap-1.5">
              <!-- Microphone Button -->
              <button
                type="button"
                (click)="toggleListening()"
                [disabled]="loading()"
                [matTooltip]="isListening() ? 'Stop Listening' : 'Voice Input (' + getLanguageLabel(selectedLanguage()) + ')'"
                class="w-9 h-9 rounded-xl flex items-center justify-center transition-all shrink-0 focus:outline-hidden disabled:opacity-50"
                [ngClass]="isListening() ? 'bg-rose-600 text-white animate-bounce' : 'bg-slate-800 text-slate-300 hover:bg-slate-700 hover:text-white border border-slate-700'">
                <mat-icon class="!w-5 !h-5 !text-[20px]">
                  {{ isListening() ? 'mic' : 'mic_none' }}
                </mat-icon>
              </button>

              <!-- Text Input -->
              <input
                type="text"
                [(ngModel)]="inputText"
                name="chatInput"
                [placeholder]="getInputPlaceholder()"
                [disabled]="loading()"
                (keydown.enter)="$event.preventDefault(); sendMessage()"
                class="flex-1 bg-slate-950 border border-slate-800 text-slate-100 text-xs rounded-xl px-3 py-2.5 focus:outline-hidden focus:border-emerald-500 placeholder-slate-500 disabled:opacity-50" />

              <!-- Send Button -->
              <button
                type="submit"
                [disabled]="loading() || !inputText.trim()"
                matTooltip="Send message"
                class="w-9 h-9 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 text-white flex items-center justify-center transition-colors shrink-0 focus:outline-hidden">
                <mat-icon class="!w-4 !h-4 !text-[18px]">send</mat-icon>
              </button>
            </form>
          </div>
        </div>
      }
    }
  `
})
export class AiAssistantComponent implements OnInit, OnDestroy {
  readonly authService = inject(AuthService);
  private readonly aiService = inject(AiService);
  private readonly conversationService = inject(AiConversationService);
  private readonly preferenceService = inject(UserPreferenceService);
  readonly languageService = inject(LanguageService);

  @ViewChild('scrollContainer') private scrollContainer?: ElementRef<HTMLDivElement>;

  isOpen = signal<boolean>(false);
  messages = signal<AiMessageItem[]>([]);
  loading = signal<boolean>(false);
  isListening = signal<boolean>(false);
  statusMessage = signal<string | null>(null);

  inputText: string = '';

  private speechRecognition: any = null;

  selectedLanguage = computed(() => this.languageService.currentLanguage() as AiLanguage);

  get taskSession() {
    return this.conversationService.activeSession;
  }

  ngOnInit(): void {
    this.resetWelcomeMessage();

    this.conversationService.sessionStarted$.subscribe((res) => {
      this.isOpen.set(true);
      this.statusMessage.set(null);
      const title = this.getTaskTitle(res.taskName);
      const initialMsg: AiMessageItem = {
        id: GuidUtils.newId(),
        sender: 'ai',
        text: `🤖 [${title}]\n\n${res.nextQuestion}`,
        timestamp: new Date()
      };
      this.messages.set([initialMsg]);
      this.scrollToBottom();
    });

    this.initSpeechRecognition();
  }

  ngOnDestroy(): void {
    if (this.speechRecognition) {
      try {
        this.speechRecognition.abort();
      } catch {}
    }
  }

  togglePanel(): void {
    this.isOpen.set(!this.isOpen());
    if (this.isOpen() && this.messages().length === 0) {
      this.resetWelcomeMessage();
    }
  }

  onLanguageChange(lang: any): void {
    if (lang && ['en', 'hi', 'gu'].includes(String(lang))) {
      this.languageService.setLanguage(lang as any);
    }
    if (this.isListening()) {
      this.stopListening();
    }
  }

  resetWelcomeMessage(): void {
    const welcomeTexts: Record<AiLanguage, string> = {
      en: 'Hello! I am your FarmKart AI assistant. How can I help you today?',
      hi: 'नमस्ते! मैं आपका FarmKart AI सहायक हूँ। आज मैं आपकी क्या मदद कर सकता हूँ?',
      gu: 'નમસ્તે! હું તમારો FarmKart AI મદદગાર છું. આજે હું તમને કેવી રીતે મદદ કરી શકું?'
    };

    if (this.messages().length === 0) {
      this.messages.set([
        {
          id: 'welcome',
          sender: 'ai',
          text: welcomeTexts[this.selectedLanguage()],
          timestamp: new Date()
        }
      ]);
    }
  }



  sendMessage(): void {
    const text = this.inputText.trim();
    if (!text || this.loading()) return;

    if (this.isListening()) {
      this.stopListening();
    }

    const userMsg: AiMessageItem = {
      id: GuidUtils.newId(),
      sender: 'user',
      text,
      timestamp: new Date()
    };

    this.messages.update(list => [...list, userMsg]);
    this.inputText = '';
    this.statusMessage.set(null);
    this.loading.set(true);
    this.scrollToBottom();

    const activeSession = this.taskSession();

    if (activeSession) {
      // AI-2 Task Mode message processing
      this.conversationService.sendMessage({
        conversationId: activeSession.conversationId,
        message: text,
        language: this.selectedLanguage()
      }).subscribe({
        next: (res) => {
          const aiMsg: AiMessageItem = {
            id: GuidUtils.newId(),
            sender: 'ai',
            text: res.nextQuestion,
            timestamp: new Date()
          };
          this.messages.update(list => [...list, aiMsg]);
          this.loading.set(false);
          this.scrollToBottom();
        },
        error: (err) => {
          this.loading.set(false);
          const errorText = err?.error?.message || err?.message || 'AI engine is temporarily unavailable.';
          this.statusMessage.set(errorText);
          this.scrollToBottom();
        }
      });
    } else {
      // AI-1 Freeform Chat Mode processing
      const historyDtos: AiChatMessageDto[] = this.messages()
        .filter(m => m.id !== 'welcome')
        .slice(-6)
        .map(m => ({
          role: m.sender === 'user' ? 'user' : 'assistant',
          content: m.text
        }));

      this.aiService.chat({
        message: text,
        language: this.selectedLanguage(),
        history: historyDtos
      }).subscribe({
        next: (res) => {
          const aiMsg: AiMessageItem = {
            id: GuidUtils.newId(),
            sender: 'ai',
            text: res.message,
            timestamp: new Date()
          };
          this.messages.update(list => [...list, aiMsg]);
          this.loading.set(false);
          this.scrollToBottom();
        },
        error: (err) => {
          this.loading.set(false);
          const errorText = err?.error?.message || err?.message || 'AI is temporarily unavailable. Please try again.';
          this.statusMessage.set(errorText);
          this.scrollToBottom();
        }
      });
    }
  }

  confirmTaskDetails(): void {
    this.conversationService.confirmAndComplete();
    const doneMsg: AiMessageItem = {
      id: GuidUtils.newId(),
      sender: 'ai',
      text: 'Thank you! Details confirmed.',
      timestamp: new Date()
    };
    this.messages.update(list => [...list, doneMsg]);
    this.scrollToBottom();
  }

  cancelTaskSession(): void {
    const session = this.taskSession();
    if (session) {
      this.conversationService.cancelConversation(session.conversationId).subscribe({
        next: () => {
          const cancelMsg: AiMessageItem = {
            id: GuidUtils.newId(),
            sender: 'ai',
            text: 'Task conversation cancelled. Changes were not saved.',
            timestamp: new Date()
          };
          this.messages.update(list => [...list, cancelMsg]);
          this.scrollToBottom();
        }
      });
    }
  }

  // --- Voice Input / Web Speech Recognition ---

  private initSpeechRecognition(): void {
    if (typeof window === 'undefined') return;
    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (SpeechRecognition) {
      this.speechRecognition = new SpeechRecognition();
      this.speechRecognition.continuous = false;
      this.speechRecognition.interimResults = true;

      this.speechRecognition.onstart = () => {
        this.isListening.set(true);
        this.statusMessage.set(null);
      };

      this.speechRecognition.onresult = (event: any) => {
        let transcript = '';
        for (let i = event.resultIndex; i < event.results.length; ++i) {
          transcript += event.results[i][0].transcript;
        }
        if (transcript.trim()) {
          this.inputText = transcript;
        }
      };

      this.speechRecognition.onerror = (event: any) => {
        this.isListening.set(false);
        if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
          this.statusMessage.set('Microphone permission is required for voice input.');
        } else if (event.error === 'language-not-supported') {
          this.statusMessage.set(`Voice input in ${this.getLanguageLabel(this.selectedLanguage())} is not supported in this browser.`);
        } else if (event.error !== 'no-speech') {
          this.statusMessage.set('Voice recognition error. Please try again.');
        }
      };

      this.speechRecognition.onend = () => {
        this.isListening.set(false);
      };
    }
  }

  toggleListening(): void {
    if (!this.speechRecognition) {
      this.statusMessage.set('Voice input is not supported in this browser.');
      return;
    }

    if (this.isListening()) {
      this.stopListening();
    } else {
      this.startListening();
    }
  }

  startListening(): void {
    if (!this.speechRecognition) return;

    const localeMap: Record<AiLanguage, string> = {
      en: 'en-IN',
      hi: 'hi-IN',
      gu: 'gu-IN'
    };

    try {
      this.speechRecognition.lang = localeMap[this.selectedLanguage()] || 'en-IN';
      this.speechRecognition.start();
    } catch {
      this.stopListening();
    }
  }

  stopListening(): void {
    if (this.speechRecognition) {
      try {
        this.speechRecognition.stop();
      } catch {}
    }
    this.isListening.set(false);
  }

  getLanguageLabel(lang: AiLanguage): string {
    switch (lang) {
      case 'hi': return 'Hindi';
      case 'gu': return 'Gujarati';
      default: return 'English';
    }
  }

  getTaskTitle(taskName: string): string {
    switch (taskName) {
      case 'create_farmer_crop': return 'Crop Assistant (Add Crop)';
      case 'update_farmer_crop': return 'Crop Assistant (Edit Crop)';
      case 'update_farmer_profile': return 'Profile Assistant (Farmer)';
      case 'update_customer_profile': return 'Profile Assistant (Customer)';
      case 'update_worker_profile': return 'Profile Assistant (Worker)';
      case 'complete_profile_test': return 'AI-2 Test Mode';
      default: return taskName || 'Form Assistant';
    }
  }

  getInputPlaceholder(): string {
    switch (this.selectedLanguage()) {
      case 'hi': return 'अपना संदेश टाइप करें...';
      case 'gu': return 'તમારો સંદેશ ટાઇપ કરો...';
      default: return 'Type your message...';
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.scrollContainer) {
        this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
      }
    }, 100);
  }
}

class GuidUtils {
  static newId(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}
