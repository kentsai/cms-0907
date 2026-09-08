import { fakeJwt } from '@app/testing/auth-testing';
import { decodeJwtPayload, mustChangePasswordFromToken, rolesFromToken } from './jwt.util';

describe('jwt.util', () => {
  describe('decodeJwtPayload', () => {
    it('decodes the payload segment, including non-ASCII values', () => {
      const payload = decodeJwtPayload(fakeJwt({ sub: 'mei', userName: '陳小美' }));

      expect(payload).toEqual({ sub: 'mei', userName: '陳小美' });
    });

    it('returns null for a missing, malformed or non-object token', () => {
      expect(decodeJwtPayload(null)).toBeNull();
      expect(decodeJwtPayload('')).toBeNull();
      expect(decodeJwtPayload('not-a-jwt')).toBeNull();
      expect(decodeJwtPayload('a.b')).toBeNull();
      expect(decodeJwtPayload('a.!!!.c')).toBeNull();
      expect(decodeJwtPayload(`h.${btoa('[1,2]')}.s`)).toBeNull();
    });
  });

  describe('mustChangePasswordFromToken', () => {
    it('is true for a boolean or string "true" claim', () => {
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: true }))).toBeTrue();
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: 'true' }))).toBeTrue();
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: 'True' }))).toBeTrue();
    });

    it('is false when the claim is absent, false, another value, or the token unreadable', () => {
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei' }))).toBeFalse();
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: false }))).toBeFalse();
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: 'false' }))).toBeFalse();
      expect(mustChangePasswordFromToken(fakeJwt({ sub: 'mei', mustChangePassword: 1 }))).toBeFalse();
      expect(mustChangePasswordFromToken(undefined)).toBeFalse();
      expect(mustChangePasswordFromToken('garbage')).toBeFalse();
    });
  });

  describe('rolesFromToken', () => {
    it('returns a single string role claim as a one-element array', () => {
      expect(rolesFromToken(fakeJwt({ role: 'Admin' }))).toEqual(['Admin']);
    });

    it('returns every entry of an array role claim', () => {
      expect(rolesFromToken(fakeJwt({ role: ['Admin', 'Editor'] }))).toEqual(['Admin', 'Editor']);
    });

    it('accepts the long ClaimTypes.Role name as well', () => {
      const token = fakeJwt({ 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Editor'] });

      expect(rolesFromToken(token)).toEqual(['Editor']);
    });

    it('returns an empty array when there is no role claim or no readable token', () => {
      expect(rolesFromToken(fakeJwt({ sub: 'mei' }))).toEqual([]);
      expect(rolesFromToken(undefined)).toEqual([]);
      expect(rolesFromToken('garbage')).toEqual([]);
    });
  });
});
