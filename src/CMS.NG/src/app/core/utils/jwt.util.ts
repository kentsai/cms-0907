/**
 * Read-only helpers for the JWT the API issues. The signature is NOT verified here — the browser
 * only needs the claims for display / menu decisions; the API re-validates every request.
 */

/** Claim names under which ASP.NET Core may serialise `ClaimTypes.Role`. */
const ROLE_CLAIM_NAMES = ['role', 'roles', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

/** Claim the API adds to a token issued to a login that used the system default password (`JwtTokenIssuer.MustChangePasswordClaim`). */
export const MUST_CHANGE_PASSWORD_CLAIM = 'mustChangePassword';

/**
 * True when the token carries the must-change-password claim: the API then answers 403 to everything except
 * the password change, so the app must keep the user on the change-password page. The claim is written as a
 * JSON boolean, but a string `"true"` is accepted as well.
 */
export function mustChangePasswordFromToken(token: string | null | undefined): boolean {
  const value = decodeJwtPayload(token)?.[MUST_CHANGE_PASSWORD_CLAIM];
  return value === true || (typeof value === 'string' && value.toLowerCase() === 'true');
}

/** Decodes the payload segment of a JWT, or returns null for anything that is not a three-part token with a JSON object payload. */
export function decodeJwtPayload(token: string | null | undefined): Record<string, unknown> | null {
  if (!token) {
    return null;
  }
  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
    const bytes = Uint8Array.from(atob(padded), c => c.charCodeAt(0));
    const payload: unknown = JSON.parse(new TextDecoder().decode(bytes));
    return payload !== null && typeof payload === 'object' && !Array.isArray(payload)
      ? (payload as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

/** The role claims of the token: a single role arrives as a string, several as an array. Empty when the token is missing or unreadable. */
export function rolesFromToken(token: string | null | undefined): string[] {
  const payload = decodeJwtPayload(token);
  if (!payload) {
    return [];
  }
  for (const name of ROLE_CLAIM_NAMES) {
    const value = payload[name];
    if (typeof value === 'string') {
      return [value];
    }
    if (Array.isArray(value)) {
      return value.filter((v): v is string => typeof v === 'string');
    }
  }
  return [];
}
