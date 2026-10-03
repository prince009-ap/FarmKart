// ─── Recommendation Foundation Models ────────────────────────────────────────
// These are the shared Angular models for the FarmKart AI Recommendation Engine.
// Future feature modules (Worker Recommendation, Machinery Recommendation, etc.)
// import from this file instead of defining their own contracts.

export interface RecommendationFactor {
  name: string;
  rawScore: number;
  weight: number;
  reason: string;
}

export interface RecommendationCandidate {
  entityId: string;
  entityType: string;
  factors: RecommendationFactor[];
  metadata?: Record<string, string>;
}

export interface RecommendationRequest {
  requestingUserId: string;
  recommendationType: string;
  candidates: RecommendationCandidate[];
  contextSummary: string;
  language?: 'en' | 'hi' | 'gu';
  maxResults?: number;
  minMatchScore?: number;
  forceDeterministicOnly?: boolean;
}

export interface RecommendationResult {
  entityId: string;
  entityType: string;
  /** Normalized match score [0–100]. Generated server-side. */
  matchScore: number;
  /** AI-assisted confidence [0.0–1.0]. Generated server-side. */
  confidenceScore: number;
  reasons: string[];
  warnings: string[];
  metadata?: Record<string, string>;
}

export interface RecommendationResponse {
  recommendationType: string;
  results: RecommendationResult[];
  /** True if AI reasoning enrichment was applied. */
  aiEnriched: boolean;
  /** Non-null when AI was skipped or fell back to deterministic scoring. */
  fallbackReason?: string | null;
}
