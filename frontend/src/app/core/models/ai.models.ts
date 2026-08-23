export type AiLanguage = 'en' | 'hi' | 'gu';

export interface AiChatMessageDto {
  role: 'user' | 'assistant';
  content: string;
}

export interface AiChatRequest {
  message: string;
  language: AiLanguage;
  history?: AiChatMessageDto[];
}

export interface AiChatResponse {
  message: string;
  language: string;
}

export interface AiMessageItem {
  id: string;
  sender: 'user' | 'ai';
  text: string;
  timestamp: Date;
  status?: 'sending' | 'success' | 'error';
}
