import { Component } from '@angular/core';
import { StudentPlanComponent } from './plan/student-plan.component';

/** The Planner page: the recommended plan first (brief §9). */
@Component({
  selector: 'app-planner-page',
  imports: [StudentPlanComponent],
  template: `<app-student-plan />`,
})
export class PlannerPage {}
