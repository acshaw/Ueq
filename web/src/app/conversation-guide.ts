import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Conversation System author guide. Static content; markup lives in conversation-guide.html. */
@Component({
  selector: 'app-conversation-guide',
  templateUrl: './conversation-guide.html',
  styles: [DOC_STYLES],
})
export class ConversationGuide {}
