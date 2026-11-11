import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CourseTitles } from './course-titles';
import {
  AvailabilityPreset,
  DeliveryPreference,
  GraduationProjection,
  PlanCheck,
  PlanningView,
  Preferences,
  SavedPlan,
  TimeWindow,
} from './planning.models';

export interface SaveResponse {
  outcome: 'Saved' | 'Invalid' | 'WarningsNotConfirmed' | 'AlreadyWithAdvisor' | 'NotFound';
  check: PlanCheck | null;
  plan: SavedPlan | null;
}

@Injectable({ providedIn: 'root' })
export class PlanningService {
  private readonly http = inject(HttpClient);
  private readonly titles = inject(CourseTitles);

  async view(availability: TimeWindow[], delivery: DeliveryPreference, maxCredits: number | null): Promise<PlanningView> {
    const v = await firstValueFrom(this.http.post<PlanningView>('/api/students/me/planning', { availability, delivery, maxCredits }));
    this.titles.register(v.courses ?? []);
    return v;
  }

  /** The student's saved preferences, then the plan built with them. */
  async currentView(): Promise<{ prefs: Preferences; view: PlanningView }> {
    const prefs = await this.preferences();
    return { prefs, view: await this.view(prefs.availability, prefs.delivery, prefs.maxCredits) };
  }

  check(sectionIds: string[], availability: TimeWindow[]): Promise<PlanCheck> {
    return firstValueFrom(this.http.post<PlanCheck>('/api/students/me/plan/check', { sectionIds, availability }));
  }

  accept(): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/students/me/plan/accept', null));
  }

  preferences(): Promise<Preferences> {
    return firstValueFrom(this.http.get<Preferences>('/api/students/me/preferences'));
  }

  savePreferences(preset: AvailabilityPreset, availability: TimeWindow[], delivery: DeliveryPreference, maxCredits: number | null): Promise<unknown> {
    return firstValueFrom(this.http.put('/api/students/me/preferences', { preset, availability, delivery, maxCredits }));
  }

  /** Autosave of the planner's unsaved selection. */
  saveDraft(sectionIds: string[]): Promise<unknown> {
    return firstValueFrom(this.http.put('/api/students/me/draft', { sectionIds }));
  }

  projection(sectionIds: string[]): Promise<GraduationProjection> {
    return firstValueFrom(this.http.post<GraduationProjection>('/api/students/me/projection', { sectionIds }));
  }

  /** Errors come back as 400/409 with the same body shape; callers read `outcome`. */
  async save(sectionIds: string[], availability: TimeWindow[], submit: boolean, acknowledgedWarnings: string[]): Promise<SaveResponse> {
    try {
      return await firstValueFrom(
        this.http.post<SaveResponse>('/api/students/me/plan', { sectionIds, availability, submit, acknowledgedWarnings }),
      );
    } catch (e: unknown) {
      const body = (e as { error?: SaveResponse })?.error;
      if (body?.outcome) return body;
      throw e;
    }
  }
}
