import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export const KINDS = ['terms', 'courses', 'programs', 'degree-plans', 'sections', 'students', 'transcripts'] as const;
export type Kind = (typeof KINDS)[number];

export interface ImportIssue {
  severity: 'Error' | 'Warning';
  line: number;
  column: string | null;
  message: string;
}

export interface ImportReport {
  kind: string;
  dryRun: boolean;
  rowsRead: number;
  created: number;
  updated: number;
  unchanged: number;
  removed: number;
  saved: boolean;
  catalogVersion: number | null;
  issues: ImportIssue[];
  errorCount: number;
  warningCount: number;
}

export interface CatalogRows {
  kind: string;
  columns: string[];
  rows: Record<string, string | null>[];
}

export interface CatalogVersion {
  number: number;
  source: 'Upload' | 'Edit' | 'Seed' | 'Removal';
  kind: string;
  fileName: string | null;
  at: string;
  actorName: string | null;
  rowsRead: number;
  created: number;
  updated: number;
  unchanged: number;
  removed: number;
}

export interface CreditRule {
  name: string;
  season: 'Fall' | 'Spring' | 'Summer' | null;
  standing: 'Freshman' | 'Sophomore' | 'Junior' | 'Senior' | null;
  minGpa: number | null;
  maxGpaExclusive: number | null;
  onProbation: boolean | null;
  minCredits: number;
  maxCredits: number;
}

export interface CreditPolicy {
  repeatGradeRule: 'LatestCounts' | 'HighestCounts';
  allowInProgressPrerequisites: boolean;
  graduatingStudentsExemptFromMinimum: boolean;
  rules: CreditRule[];
}

export interface CreditPolicyPreview {
  termName: string;
  studentsChecked: number;
  changes: {
    studentId: string;
    studentName: string;
    standing: string;
    gpa: number | null;
    onProbation: boolean;
    beforeRule: string | null;
    beforeMin: number | null;
    beforeMax: number | null;
    afterRule: string | null;
    afterMin: number | null;
    afterMax: number | null;
  }[];
  problems: string[];
}

export interface StaffUser {
  id: string;
  username: string;
  displayName: string;
  email: string | null;
  role: 'Advisor' | 'Admin';
  isActive: boolean;
  lastSignInAt: string | null;
  advisees: number;
  hasPassword: boolean;
}

export interface SsoSettings {
  enabled: boolean;
  tenantId: string | null;
  clientId: string | null;
  clientSecret: string | null;
  localSignInEnabled: boolean;
  effectiveEnabled: boolean;
  effectiveLocalSignIn: boolean;
  lastTestOkAt: string | null;
}

export interface AiSettings {
  enabled: boolean;
  provider: 'anthropic' | 'openai-compatible' | 'mock';
  model: string;
  baseUrl: string | null;
  apiKey: string | null;
  timeoutSeconds: number;
  hourlyLimitPerUser: number;
}

export interface TestResult {
  ok: boolean;
  message: string;
}

/** Problem-details title from an error response, if any. */
export function problemTitle(e: unknown): string | null {
  return e instanceof HttpErrorResponse ? ((e.error as { title?: string } | null)?.title ?? null) : null;
}

/** The admin API (brief §11). Imports and edits return the report for 200 and 422 alike. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  rows(kind: Kind): Promise<CatalogRows> {
    return firstValueFrom(this.http.get<CatalogRows>(`/api/admin/catalog/${kind}`));
  }

  editRows(kind: Kind, rows: Record<string, string | null>[], dryRun: boolean): Promise<ImportReport> {
    return this.report(this.http.post<ImportReport>(`/api/admin/catalog/${kind}/rows`, { rows, dryRun }));
  }

  upload(kind: Kind, file: File, dryRun: boolean): Promise<ImportReport> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.report(this.http.post<ImportReport>(`/api/admin/imports/${kind}?dryRun=${dryRun}`, form));
  }

  checkPrerequisite(courseCode: string, expression: string): Promise<{ ok: boolean; canonical: string | null; problems: string[] }> {
    return firstValueFrom(this.http.post<{ ok: boolean; canonical: string | null; problems: string[] }>('/api/admin/catalog/prerequisites/check', { courseCode, expression }));
  }

  removeDegreePlan(program: string, year: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/admin/catalog/degree-plans/${encodeURIComponent(program)}/${year}`));
  }

  versions(): Promise<CatalogVersion[]> {
    return firstValueFrom(this.http.get<CatalogVersion[]>('/api/catalog/versions?take=100'));
  }

  /** Downloads a file through the authenticated API and hands it to the browser. */
  async download(url: string, fileName: string): Promise<void> {
    const blob = await firstValueFrom(this.http.get(url, { responseType: 'blob' }));
    const href = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = href;
    a.download = fileName;
    a.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  }

  creditPolicy(): Promise<CreditPolicy> {
    return firstValueFrom(this.http.get<CreditPolicy>('/api/admin/credit-policy'));
  }

  previewCreditPolicy(p: CreditPolicy): Promise<CreditPolicyPreview> {
    return firstValueFrom(this.http.post<CreditPolicyPreview>('/api/admin/credit-policy/preview', p));
  }

  async saveCreditPolicy(p: CreditPolicy): Promise<CreditPolicyPreview> {
    try {
      return await firstValueFrom(this.http.put<CreditPolicyPreview>('/api/admin/credit-policy', p));
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 422) return e.error as CreditPolicyPreview;
      throw e;
    }
  }

  users(): Promise<StaffUser[]> {
    return firstValueFrom(this.http.get<StaffUser[]>('/api/admin/users'));
  }

  createUser(body: { username: string; displayName: string; email: string | null; role: 'Advisor' | 'Admin'; password: string }): Promise<StaffUser> {
    return firstValueFrom(this.http.post<StaffUser>('/api/admin/users', body));
  }

  setPassword(id: string, password: string): Promise<unknown> {
    return firstValueFrom(this.http.put(`/api/admin/users/${id}/password`, { password }));
  }

  setActive(id: string, active: boolean): Promise<unknown> {
    return firstValueFrom(this.http.put(`/api/admin/users/${id}/active`, { active }));
  }

  reassign(fromAdvisorUserId: string, toAdvisorUserId: string, studentIds: string[]): Promise<{ moved: number }> {
    return firstValueFrom(this.http.post<{ moved: number }>('/api/admin/users/reassign', { fromAdvisorUserId, toAdvisorUserId, studentIds }));
  }

  sso(): Promise<SsoSettings> {
    return firstValueFrom(this.http.get<SsoSettings>('/api/admin/settings/sso'));
  }

  saveSso(body: { enabled: boolean; tenantId: string | null; clientId: string | null; clientSecret: string | null; localSignInEnabled: boolean; confirmSsoOnly: boolean }): Promise<SsoSettings> {
    return firstValueFrom(this.http.put<SsoSettings>('/api/admin/settings/sso', body));
  }

  testSso(): Promise<TestResult> {
    return firstValueFrom(this.http.post<TestResult>('/api/admin/settings/sso/test', null));
  }

  ai(): Promise<AiSettings> {
    return firstValueFrom(this.http.get<AiSettings>('/api/admin/settings/ai'));
  }

  saveAi(body: Omit<AiSettings, 'apiKey'> & { apiKey: string | null }): Promise<AiSettings> {
    return firstValueFrom(this.http.put<AiSettings>('/api/admin/settings/ai', body));
  }

  testAi(): Promise<TestResult> {
    return firstValueFrom(this.http.post<TestResult>('/api/admin/settings/ai/test', null));
  }

  private async report(obs: ReturnType<HttpClient['post']>): Promise<ImportReport> {
    try {
      return (await firstValueFrom(obs)) as ImportReport;
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 422) return e.error as ImportReport;
      throw e;
    }
  }
}
