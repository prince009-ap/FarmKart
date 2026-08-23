export type AiFieldType = 'text' | 'number' | 'decimal' | 'phone' | 'date' | 'boolean' | 'select' | 'textarea';

export interface AiFormFieldDefinition {
  name: string;
  label: string;
  type: AiFieldType;
  required: boolean;
  description?: string;
  options?: string[];
}

export interface AiTaskContext {
  taskName: string;
  pageName: string;
  language?: 'en' | 'hi' | 'gu';
  fields?: AiFormFieldDefinition[];
}

export interface StartAiConversationRequest {
  taskName: string;
  pageName: string;
  language?: string;
  fields?: AiFormFieldDefinition[];
  initialData?: Record<string, string | null>;
}

export interface SendAiConversationMessageRequest {
  conversationId: string;
  message: string;
  language?: string;
}

export interface CancelAiConversationRequest {
  conversationId: string;
}

export interface AiExtractedFieldDto {
  fieldName: string;
  value: string;
  isValid: boolean;
  validationMessage?: string;
}

export interface AiConversationStateResponse {
  conversationId: string;
  taskName: string;
  pageName: string;
  language: string;
  status: 'Collecting' | 'ReadyForConfirmation' | 'Completed' | 'Cancelled';
  nextQuestion: string;
  currentField?: string | null;
  fieldValues: Record<string, string | null>;
  recentlyExtractedFields: AiExtractedFieldDto[];
  missingRequiredFields: string[];
  missingOptionalFields: string[];
  summaryText?: string | null;
}

export interface AiFieldUpdatedEvent {
  field: string;
  value: string | null;
  taskName: string;
}
