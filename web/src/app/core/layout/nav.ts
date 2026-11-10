import { Role } from '../auth/auth.models';

export interface NavItem {
  /** Translation key under nav.* */
  key: string;
  icon: string;
  link: string;
  /** Exact match for the role's home link. */
  exact?: boolean;
  /** Name of a live count shown as a badge (filled by the shell). */
  badge?: 'queue';
}

/** The left rail per role (brief §9). Phones show the first four in the bottom bar and all in the drawer. */
export const NAV: Record<Role, NavItem[]> = {
  Student: [
    { key: 'home', icon: 'home', link: '/student', exact: true },
    { key: 'availability', icon: 'schedule', link: '/student/availability' },
    { key: 'planner', icon: 'event_note', link: '/student/planner' },
    { key: 'graduation', icon: 'school', link: '/student/graduation' },
    { key: 'myPlan', icon: 'assignment_turned_in', link: '/student/plan' },
    { key: 'messages', icon: 'mail', link: '/student/messages' },
  ],
  Advisor: [
    { key: 'home', icon: 'home', link: '/advisor', exact: true },
    { key: 'queue', icon: 'inbox', link: '/advisor/plans', badge: 'queue' },
    { key: 'advisees', icon: 'groups', link: '/advisor/students' },
    { key: 'messages', icon: 'mail', link: '/advisor/messages' },
  ],
  Admin: [
    { key: 'home', icon: 'home', link: '/admin', exact: true },
    { key: 'messages', icon: 'mail', link: '/admin/messages' },
  ],
};
