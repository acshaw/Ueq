import { Component } from '@angular/core';
import { DOC_STYLES } from './doc-styles';

/** Build & Deploy guide (6.2 / 6.4). Static content; markup lives in build-deploy-guide.html. */
@Component({
  selector: 'app-build-deploy-guide',
  templateUrl: './build-deploy-guide.html',
  styles: [DOC_STYLES, `
    .doc .tag.local { background: #eafaf1; color: #1e7e46; border: 1px solid #c8ecd7; }
    .doc .tag.aws { background: #fff2e0; color: #a05a00; border: 1px solid #f5d9ad; }
  `],
})
export class BuildDeployGuide {}
