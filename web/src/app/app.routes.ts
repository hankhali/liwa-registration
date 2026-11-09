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
        path: 'advisor',
        canActivate: [roleGuard('Advisor')],
        title: 'advisor.home.title',
        loadComponent: () => import('./features/advisor/advisor-home.page').then((m) => m.AdvisorHomePage),
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
