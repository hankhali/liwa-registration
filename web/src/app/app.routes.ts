import { Routes } from '@angular/router';
import { authGuard, guestGuard, homeRedirect, roleGuard } from './core/auth/auth.guards';
import { ShellComponent } from './core/layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'login.title',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', canMatch: [homeRedirect], children: [] },
      {
        path: 'student',
        canActivate: [roleGuard('Student')],
        title: 'student.home.title',
        loadComponent: () => import('./features/student/student-home.page').then((m) => m.StudentHomePage),
      },
      {
        path: 'student/availability',
        canActivate: [roleGuard('Student')],
        title: 'nav.studentFreeTime',
        loadComponent: () => import('./features/student/availability.page').then((m) => m.AvailabilityPage),
      },
      {
        path: 'student/planner',
        canActivate: [roleGuard('Student')],
        title: 'nav.studentMyPlan',
        loadComponent: () => import('./features/student/planner.page').then((m) => m.PlannerPage),
      },
      {
        path: 'student/graduation',
        canActivate: [roleGuard('Student')],
        title: 'nav.graduationShort',
        loadComponent: () => import('./features/student/graduation.page').then((m) => m.GraduationPage),
      },
      {
        path: 'student/plan',
        canActivate: [roleGuard('Student')],
        title: 'myPlan.heading',
        loadComponent: () => import('./features/student/my-plan.page').then((m) => m.MyPlanPage),
      },
      ...(['student', 'advisor', 'admin'] as const).map((area) => ({
        path: `${area}/messages`,
        canActivate: [roleGuard(area === 'student' ? 'Student' : area === 'advisor' ? 'Advisor' : 'Admin')],
        title: 'nav.messages',
        loadComponent: () => import('./features/messages/messages.page').then((m) => m.MessagesPage),
      })),
      {
        path: 'advisor',
        canActivate: [roleGuard('Advisor')],
        title: 'advisor.home.title',
        loadComponent: () => import('./features/advisor/advisor-home.page').then((m) => m.AdvisorHomePage),
      },
      {
        path: 'advisor/students',
        canActivate: [roleGuard('Advisor')],
        title: 'nav.advisees',
        loadComponent: () => import('./features/advisor/advisees.page').then((m) => m.AdviseesPage),
      },
      {
        path: 'advisor/plans',
        canActivate: [roleGuard('Advisor')],
        title: 'advisor.queue.title',
        loadComponent: () => import('./features/advisor/advisor-queue.page').then((m) => m.AdvisorQueuePage),
      },
      {
        path: 'advisor/plans/:planId',
        canActivate: [roleGuard('Advisor')],
        title: 'advisor.review.title',
        loadComponent: () => import('./features/advisor/plan-review.page').then((m) => m.PlanReviewPage),
      },
      {
        path: 'advisor/students/:studentUserId',
        canActivate: [roleGuard('Advisor')],
        title: 'advisor.record.title',
        loadComponent: () => import('./features/advisor/student-record.page').then((m) => m.StudentRecordPage),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('Admin')],
        title: 'admin.home.title',
        loadComponent: () => import('./features/admin/admin-home.page').then((m) => m.AdminHomePage),
      },
      {
        path: 'admin/catalog',
        canActivate: [roleGuard('Admin')],
        title: 'nav.catalog',
        loadComponent: () => import('./features/admin/catalog.page').then((m) => m.CatalogPage),
      },
      {
        path: 'admin/imports',
        canActivate: [roleGuard('Admin')],
        title: 'nav.imports',
        loadComponent: () => import('./features/admin/imports.page').then((m) => m.ImportsPage),
      },
      {
        path: 'admin/users',
        canActivate: [roleGuard('Admin')],
        title: 'nav.users',
        loadComponent: () => import('./features/admin/users.page').then((m) => m.UsersPage),
      },
      {
        path: 'admin/settings',
        canActivate: [roleGuard('Admin')],
        title: 'nav.settings',
        loadComponent: () => import('./features/admin/settings.page').then((m) => m.SettingsPage),
      },
      {
        path: 'admin/ai-usage',
        canActivate: [roleGuard('Admin')],
        title: 'nav.aiUsage',
        loadComponent: () => import('./features/admin/ai-usage.page').then((m) => m.AiUsagePage),
      },
      {
        path: 'advisor/versions',
        canActivate: [roleGuard('Advisor')],
        title: 'admin.versions.heading',
        loadComponent: () => import('./features/advisor/versions.page').then((m) => m.VersionsPage),
      },
      {
        path: 'forbidden',
        title: 'errors.forbidden.title',
        loadComponent: () => import('./features/errors/forbidden.page').then((m) => m.ForbiddenPage),
      },
      {
        path: '**',
        title: 'errors.notFound.title',
        loadComponent: () => import('./features/errors/not-found.page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
