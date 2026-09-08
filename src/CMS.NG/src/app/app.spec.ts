import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideNoopAnimations(), MessageService, ConfirmationService]
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

  it('should list AppUser under the Admin menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('使用者 AppUser');
  });

  it('should list PublishStatus under the Admin menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('發布狀態 PublishStatus');
  });

  it('should list Partner under the Course menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('課程管理 Course');
    expect(sidebarText).toContain('合作夥伴 Partner');
  });

  it('should list CourseGroup under the Course menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('課程群組 CourseGroup');
  });

  it('should list Course under the Course menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('課程 Course');
  });

  it('should list FeaturedPromoItem under the Home menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('首頁 Home');
    expect(sidebarText).toContain('上稿作業 FeaturedPromoItem');
  });

  it('should list Certification under the Course menu group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const sidebarText = (fixture.nativeElement as HTMLElement).querySelector('.app-sidebar')?.textContent ?? '';
    expect(sidebarText).toContain('認證 Certification');
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
