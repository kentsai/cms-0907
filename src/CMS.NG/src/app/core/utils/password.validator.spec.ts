import { FormControl, FormGroup } from '@angular/forms';
import {
  PASSWORD_POLICY_MESSAGE,
  meetsPasswordPolicy,
  passwordPolicyValidator,
  passwordsMatchValidator
} from './password.validator';

describe('password.validator', () => {
  describe('meetsPasswordPolicy', () => {
    const accepted = [
      'Abcdefg1', // upper + lower + digit, exactly 8
      'abcdefg1!', // lower + digit + symbol
      'ABCDEFG1!', // upper + digit + symbol
      'Abcdefg!', // upper + lower + symbol
      'Ab1!Ab1!', // all four
      'Ab1~Ab1~', // symbol at the top of the printable range
      'Ab1 Ab1 ' // spaces allowed, still three classes
    ];
    const rejected = [
      '',
      'Ab1!', // too short
      'Abcdef1', // 7 characters
      'abcdefgh', // 1 class
      'ABCDEFGH',
      '12345678',
      '!@#$%^&*',
      'abcdefg1', // 2 classes
      'Abcdefgh',
      'ABCDEFG1',
      'abcdefg!',
      '1234567!',
      'abcd 123', // whitespace is not a symbol
      'abcd中文123', // CJK is not a symbol
      'ａｂｃＡＢＣ１２３' // full-width characters are not ASCII classes
    ];

    for (const password of accepted) {
      it(`accepts "${password}"`, () => expect(meetsPasswordPolicy(password)).toBeTrue());
    }
    for (const password of rejected) {
      it(`rejects "${password}"`, () => expect(meetsPasswordPolicy(password)).toBeFalse());
    }

    it('rejects null and undefined', () => {
      expect(meetsPasswordPolicy(null)).toBeFalse();
      expect(meetsPasswordPolicy(undefined)).toBeFalse();
    });
  });

  describe('passwordPolicyValidator', () => {
    it('leaves a blank value to `required`', () => {
      expect(passwordPolicyValidator(new FormControl(''))).toBeNull();
      expect(passwordPolicyValidator(new FormControl(null))).toBeNull();
    });

    it('reports passwordPolicy for a weak value and nothing for a compliant one', () => {
      expect(passwordPolicyValidator(new FormControl('abcdefgh'))).toEqual({ passwordPolicy: true });
      expect(passwordPolicyValidator(new FormControl('Abcdefg1'))).toBeNull();
    });
  });

  describe('passwordsMatchValidator', () => {
    function group(newPassword: string, confirm: string): FormGroup {
      return new FormGroup(
        {
          newPassword: new FormControl(newPassword),
          confirmNewPassword: new FormControl(confirm)
        },
        { validators: passwordsMatchValidator('newPassword', 'confirmNewPassword') }
      );
    }

    it('is valid while the confirmation is still empty', () => {
      expect(group('Abcdefg1', '').errors).toBeNull();
    });

    it('is valid when both values are identical', () => {
      expect(group('Abcdefg1', 'Abcdefg1').errors).toBeNull();
    });

    it('reports passwordMismatch for a different, differently-cased or padded confirmation', () => {
      expect(group('Abcdefg1', 'Abcdefg2').errors).toEqual({ passwordMismatch: true });
      expect(group('Abcdefg1', 'abcdefg1').errors).toEqual({ passwordMismatch: true });
      expect(group('Abcdefg1', 'Abcdefg1 ').errors).toEqual({ passwordMismatch: true });
    });
  });

  it('exposes the bilingual policy message', () => {
    expect(PASSWORD_POLICY_MESSAGE).toContain('密碼長度至少需 8 碼');
    expect(PASSWORD_POLICY_MESSAGE).toContain('大寫英文／小寫英文／數字／符號');
    expect(PASSWORD_POLICY_MESSAGE).toContain('uppercase / lowercase / digit / symbol');
  });
});
