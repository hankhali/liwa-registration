import { StudentSummary } from '../academic/academic.models';
import { PlanCheck, PlanStatus, PlanningView, Reason, TimeWindow } from './planning.models';

export interface PlanEvent {
  at: string;
  actorName: string;
  actorRole: string;
  action: 'submitted' | 'saved' | 'reviewing' | 'approved' | 'rejected' | 'proposed' | 'accepted';
  status: PlanStatus;
  version: number | null;
  note: string | null;
  added: string[];
  removed: string[];
}

export interface QueueItem {
  planId: string;
  studentUserId: string;
  studentName: string;
  studentId: string;
  programCode: string;
  programName: string;
  programNameAr: string | null;
  status: PlanStatus;
  version: number;
  credits: number;
  warningCount: number;
  updatedAt: string;
  onProbation: boolean;
  urgent: boolean;
}

export interface ConfirmedWarning extends Reason {
  key: string;
}

export interface TermProjectionLite {
  graduationTerm: string | null;
  termsRemaining: number;
}

export interface PlanReview {
  planId: string;
  studentUserId: string;
  status: PlanStatus;
  version: number;
  student: StudentSummary;
  studentName: string;
  planning: PlanningView & { history: PlanEvent[] };
  sectionIds: string[];
  liveCheck: PlanCheck;
  confirmedWarnings: ConfirmedWarning[];
  thisPlan: { withSummer: TermProjectionLite; withoutSummer: TermProjectionLite };
  history: PlanEvent[];
}

export interface TranscriptLine {
  termCode: string;
  courseCode: string;
  title: string;
  titleAr: string | null;
  credits: number;
  grade: string | null;
  status: 'Completed' | 'InProgress' | 'Withdrawn' | 'Transfer';
}

export interface StudentRecordView {
  userId: string;
  studentName: string;
  summary: StudentSummary;
  transcript: TranscriptLine[];
  currentPlanStatus: PlanStatus | null;
  currentPlanId: string | null;
}

export interface NotificationItem {
  id: string;
  createdAt: string;
  messageKey: string;
  parameters: string | null;
  link: string | null;
  read: boolean;
}

export interface NotificationList {
  unread: number;
  items: NotificationItem[];
}

/** Statuses an advisor can decide on. */
export const DECIDABLE: PlanStatus[] = ['Submitted', 'AdvisorReviewing', 'NeedsReview'];

export function availabilityText(windows: TimeWindow[] | null | undefined): TimeWindow[] {
  return windows ?? [];
}
