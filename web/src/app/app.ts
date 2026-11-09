import { Directionality } from '@angular/cdk/bidi';
import { Component, effect, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class App {
  constructor() {
    // Keep CDK/Material (menus, overlays, sliders) in step with the page direction.
    const lang = inject(LanguageService);
    const dir = inject(Directionality);
    effect(() => {
      const value = lang.dir();
      if (dir.value !== value) {
        dir.valueSignal.set(value);
        dir.change.emit(value);
      }
    });
  }
}
