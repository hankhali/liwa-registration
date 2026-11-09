export type Role = 'Student' | 'Advisor' | 'Admin';

export interface UserProfile {
  id: string;
  username: string;
  displayName: string;
  role: Role;
  universityId: string | null;
}

export interface SignInResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

export interface Session {
  token: string;
  expiresAt: number; // epoch ms
  user: UserProfile;
}

/** Where each role lands after signing in. */
export const HOME_BY_ROLE: Record<Role, string> = {
  Student: '/student',
  Advisor: '/advisor',
  Admin: '/admin',
};

export type SignOutReason = 'user' | 'expired';
