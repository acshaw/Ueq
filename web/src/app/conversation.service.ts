import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { GridColumn } from './shared/content-grid';

export interface ItemAmount { itemId: string; quantity: number; }
export interface FactionHit { factionId: string; delta: number; }

export interface ConversationKeyword {
  keyword: string;
  mode: number;              // 0 Passive, 1 Active
  isOpener: boolean;
  endsConversation: boolean;
  requiresUnlock: boolean;
  response: string;
  requiredFactionId: string | null;
  requiredStanding: string | null;
  unlocks: string[];

  // 3.2 quest transaction bundle
  requiredCopper: number;
  requiredItems: ItemAmount[];
  rewardXp: number;
  rewardCopper: number;
  rewardItems: ItemAmount[];
  factionHits: FactionHit[];
}

export interface ConversationSet {
  setId: string;
  displayName: string;
  keywords: ConversationKeyword[];
}

export function emptyKeyword(): ConversationKeyword {
  return {
    keyword: '', mode: 0, isOpener: false, endsConversation: false, requiresUnlock: false,
    response: '', requiredFactionId: null, requiredStanding: null, unlocks: [],
    requiredCopper: 0, requiredItems: [], rewardXp: 0, rewardCopper: 0, rewardItems: [], factionHits: [],
  };
}

export function emptySet(): ConversationSet {
  return { setId: '', displayName: '', keywords: [] };
}

/** Grid columns for the Conversation Set index (2.1.1 AF5). */
export const CONVERSATION_GRID_COLUMNS: GridColumn<ConversationSet>[] = [
  { header: 'ID', accessor: s => s.setId },
  { header: 'Name', accessor: s => s.displayName },
  { header: 'Keywords', accessor: s => s.keywords.length },
];
export const CONVERSATION_SEARCH_FIELDS: (keyof ConversationSet)[] = ['setId', 'displayName'];

@Injectable({ providedIn: 'root' })
export class ConversationService {
  private readonly base = 'http://localhost:5144/api/conversations';
  private readonly http = inject(HttpClient);

  getAll(): Observable<ConversationSet[]> { return this.http.get<ConversationSet[]>(this.base); }
  create(s: ConversationSet): Observable<ConversationSet> { return this.http.post<ConversationSet>(this.base, s); }
  update(s: ConversationSet): Observable<ConversationSet> { return this.http.put<ConversationSet>(`${this.base}/${s.setId}`, s); }
  delete(setId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${setId}`); }
}
