import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Combat Pipeline author guide (5.1 / 2.12). Static content; markup lives in combat-guide.html. */
@Component({
  selector: 'app-combat-guide',
  templateUrl: './combat-guide.html',
  styles: [DOC_STYLES],
})
export class CombatGuide {}
