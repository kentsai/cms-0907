import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { seedSignedInUser } from '@app/testing/auth-testing';
import { ChangePasswordComponent } from './change-password.component';

describe('ChangePasswordComponent', () => {
  let fixture: ComponentFixture<ChangePasswordComponent>;

  async function mount(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ChangePasswordComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(ChangePasswordComponent);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const notice = () => el().querySelector('[data-testid="default-password-notice"]');

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('explains why the user is here when the session signed in with the default password', async () => {
    seedSignedInUser(['Editor'], '王大明', true);
    await mount();

    expect(el().textContent).toContain('變更密碼 Change Password');
    expect(notice()?.textContent).toContain('您目前使用預設密碼登入，請先變更密碼後再繼續使用系統');
    expect(notice()?.textContent).toContain('signed in with the default password');
  });

  it('shows the form without the notice for an ordinary session', async () => {
    seedSignedInUser(['Editor'], '王大明');
    await mount();

    expect(notice()).toBeNull();
    expect(el().querySelector('app-change-password-form')).not.toBeNull();
  });

  it('hosts the shared form with its three masked inputs', async () => {
    seedSignedInUser(['Editor'], '王大明', true);
    await mount();

    const inputs = el().querySelectorAll<HTMLInputElement>('#passwordForm input');
    expect(Array.from(inputs, i => i.id)).toEqual(['currentPassword', 'newPassword', 'confirmNewPassword']);
    expect(Array.from(inputs).every(i => i.type === 'password')).toBeTrue();
  });
});
