import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Abilities author guide (M2.9). Static content; markup lives in abilities-guide.html. */
@Component({
  selector: 'app-abilities-guide',
  templateUrl: './abilities-guide.html',
  styles: [DOC_STYLES],
})
export class AbilitiesGuide {}
