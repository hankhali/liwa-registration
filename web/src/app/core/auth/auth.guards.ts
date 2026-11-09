import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

/** Only for signed-in users; others go to the sign-in page and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.token()) return true;
  return inject(Router).createUrlTree(['/login'], state.url && state.url !== '/' ? { queryParams: { returnUrl: state.url } } : {});
};

/** Only for the given roles; a signed-in user with another role sees the "not allowed" page. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.token()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    const role = auth.role();
    return role && roles.includes(role) ? true : router.createUrlTree(['/forbidden']);
  };
}

/** The sign-in page is only for signed-out users. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.token() ? inject(Router).parseUrl(auth.homeUrl()) : true;
};

/** "/" sends each role to its own home. */
export const homeRedirect: CanMatchFn = () => {
  const auth = inject(AuthService);
  return inject(Router).parseUrl(auth.token() ? auth.homeUrl() : '/login');
};
