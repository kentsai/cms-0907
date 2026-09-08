/** Body of `POST /api/auth/login`. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/**
 * Profile returned by a successful login and kept in **session** storage for the life of the tab
 * (`AUTH_PROFILE_KEY` in `auth.service.ts`). Roles are not a member: they are claims inside the token.
 */
export interface UserProfile {
  userId: string;
  userName: string;
  /** HS256 JWT; sent as `Authorization: Bearer <token>` on every API call. */
  accessToken: string;
}

/** Body of `PUT /api/auth/profile` — only the UserName; the user is the token subject. */
export interface UpdateProfileRequest {
  userName: string;
}

/** Response of `PUT /api/auth/profile`. */
export interface ProfileResponse {
  userId: string;
  userName: string;
  roles: string[];
}

/**
 * Body of `POST /api/auth/change-password`. Plain-text passwords in, nothing out: the API hashes the new one,
 * and no hash is ever sent to or from the browser. The user is the token subject (no `userId` member).
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}
