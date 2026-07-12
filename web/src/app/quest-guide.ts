import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Quest Turn-Ins & Rewards author guide (3.2). Static content; markup lives in quest-guide.html. */
@Component({
  selector: 'app-quest-guide',
  templateUrl: './quest-guide.html',
  styles: [DOC_STYLES],
})
export class QuestGuide {}
