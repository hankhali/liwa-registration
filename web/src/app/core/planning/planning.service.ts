import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DeliveryPreference, PlanCheck, PlanningView, SavedPlan, TimeWindow } from './planning.models';

export interface SaveResponse {
  outcome: 'Saved' | 'Invalid' | 'WarningsNotConfirmed' | 'AlreadyWithAdvisor' | 'NotFound';
  check: PlanCheck | null;
  plan: SavedPlan | null;
}

@Injectable({ providedIn: 'root' })
export class PlanningService {
  private readonly http = inject(HttpClient);

  view(availability: TimeWindow[], delivery: DeliveryPreference, maxCredits: number | null): Promise<PlanningView> {
    return firstValueFrom(this.http.post<PlanningView>('/api/students/me/planning', { availability, delivery, maxCredits }));
  }

  check(sectionIds: string[], availability: TimeWindow[]): Promise<PlanCheck> {
    return firstValueFrom(this.http.post<PlanCheck>('/api/students/me/plan/check', { sectionIds, availability }));
  }

  accept(): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/students/me/plan/accept', null));
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
