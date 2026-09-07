import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideNoopAnimations()]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the application title in the topbar', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.app-topbar__title')?.textContent).toContain('CMS');
  });

  it('should render the Admin / AppRole sidebar menu', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('系統管理 Admin');
  });

  it('should toggle the sidebar collapsed state', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const shell = (fixture.nativeElement as HTMLElement).querySelector('.app-shell')!;
    expect(shell.classList).not.toContain('app-shell--collapsed');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.app-topbar__toggle')!
      .click();
    fixture.detectChanges();

    expect(shell.classList).toContain('app-shell--collapsed');
  });
});
