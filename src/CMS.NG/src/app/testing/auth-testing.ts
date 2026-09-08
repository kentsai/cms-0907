import { LoginResponse, UserProfile } from '@core/models/auth.model';
import { AUTH_PROFILE_KEY } from '@core/services/auth.service';

/**
 * Test-only helpers for signed-in state. Not referenced by app code, so nothing here ends up in the bundle.
 */

function base64Url(value: unknown): string {
  const bytes = new TextEncoder().encode(JSON.stringify(value));
  const binary = Array.from(bytes, b => String.fromCharCode(b)).join('');
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** An unsigned three-part JWT with the given payload — enough for the frontend, which never verifies signatures. */
export function fakeJwt(payload: Record<string, unknown>): string {
  return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(payload)}.test-signature`;
}

/**
 * A profile whose token carries the given roles, shaped like `POST /api/auth/login` returns it. With
 * `mustChangePassword` the token also carries the claim the API adds to a login made with the default password.
 */
export function fakeProfile(
  roles: string[] = ['Admin'],
  userName = '陳小美',
  userId = 'mei',
  mustChangePassword = false
): UserProfile {
  const role = roles.length === 1 ? roles[0] : roles; // .NET emits one role as a string, several as an array
  const claims: Record<string, unknown> = { sub: userId, userId, userName };
  if (roles.length > 0) {
    claims['role'] = role;
  }
  if (mustChangePassword) {
    claims['mustChangePassword'] = true;
  }
  return { userId, userName, accessToken: fakeJwt(claims) };
}

/** `POST /api/auth/login` body for the given profile, as the API sends it (the flag is also inside the token). */
export function fakeLoginResponse(roles: string[] = ['Admin'], mustChangePassword = false): LoginResponse {
  return { ...fakeProfile(roles, '陳小美', 'mei', mustChangePassword), mustChangePassword };
}

/** Puts a signed-in profile into session storage before the TestBed / AuthService is created. */
export function seedSignedInUser(roles: string[] = ['Admin'], userName = '陳小美', mustChangePassword = false): UserProfile {
  const profile = fakeProfile(roles, userName, 'mei', mustChangePassword);
  sessionStorage.setItem(AUTH_PROFILE_KEY, JSON.stringify(profile));
  return profile;
}
