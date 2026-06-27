import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
  };
}

export function emptySet(): ConversationSet {
  return { setId: '', displayName: '', keywords: [] };
}

@Injectable({ providedIn: 'root' })
export class ConversationService {
  private readonly base = 'http://localhost:5144/api/conversations';
  private readonly http = inject(HttpClient);

  getAll(): Observable<ConversationSet[]> { return this.http.get<ConversationSet[]>(this.base); }
  create(s: ConversationSet): Observable<ConversationSet> { return this.http.post<ConversationSet>(this.base, s); }
  update(s: ConversationSet): Observable<ConversationSet> { return this.http.put<ConversationSet>(`${this.base}/${s.setId}`, s); }
  delete(setId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${setId}`); }
}
