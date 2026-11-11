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
  /** Phone bottom bar: shown there, under this shorter label (translation key under nav.*). */
  bottom?: string;
}

/**
 * The left rail per role (brief §9). Phones: students get exactly Home / Plan / Graduation / Messages in the bottom
 * bar (UX pass); other roles get their first four plus "More". Everything is also in the drawer (top-bar menu).
 */
export const NAV: Record<Role, NavItem[]> = {
  Student: [
    { key: 'home', icon: 'home', link: '/student', exact: true, bottom: 'home' },
    { key: 'availability', icon: 'schedule', link: '/student/availability' },
    { key: 'planner', icon: 'event_note', link: '/student/planner', bottom: 'plan' },
    { key: 'graduation', icon: 'school', link: '/student/graduation', bottom: 'graduationShort' },
    { key: 'myPlan', icon: 'assignment_turned_in', link: '/student/plan' },
    { key: 'messages', icon: 'mail', link: '/student/messages', bottom: 'messages' },
  ],
  Advisor: [
    { key: 'home', icon: 'home', link: '/advisor', exact: true },
    { key: 'queue', icon: 'inbox', link: '/advisor/plans', badge: 'queue' },
    { key: 'advisees', icon: 'groups', link: '/advisor/students' },
    { key: 'messages', icon: 'mail', link: '/advisor/messages' },
    { key: 'versions', icon: 'history', link: '/advisor/versions' },
  ],
  Admin: [
    { key: 'home', icon: 'home', link: '/admin', exact: true },
    { key: 'catalog', icon: 'menu_book', link: '/admin/catalog' },
    { key: 'imports', icon: 'upload_file', link: '/admin/imports' },
    { key: 'users', icon: 'manage_accounts', link: '/admin/users' },
    { key: 'settings', icon: 'settings', link: '/admin/settings' },
    { key: 'aiUsage', icon: 'insights', link: '/admin/ai-usage' },
    { key: 'messages', icon: 'mail', link: '/admin/messages' },
  ],
};
