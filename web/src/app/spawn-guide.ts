import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Spawn System author guide. Static content; markup lives in documentation.html. */
@Component({
  selector: 'app-spawn-guide',
  templateUrl: './documentation.html',
  styles: [DOC_STYLES],
})
export class SpawnGuide {}
