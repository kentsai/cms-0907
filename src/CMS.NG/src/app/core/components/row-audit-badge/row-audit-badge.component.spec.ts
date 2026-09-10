import { Component, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { environment } from '@environments/environment';
import { RowAudit } from '@core/models/row-audit.model';
import { RowAuditBadgeComponent, toUtcIso } from './row-audit-badge.component';

@Component({
  imports: [RowAuditBadgeComponent],
  template: `<app-row-audit-badge [tableName]="tableName()" [pkid]="pkid()" />`
})
class HostComponent {
  readonly tableName = signal('Course');
  readonly pkid = signal<string | number | null>(123);
}

describe('RowAuditBadgeComponent', () => {
  const url = `${environment.apiBaseUrl}/row-audits`;
  let fixture: ComponentFixture<HostComponent>;
  let http: HttpTestingController;

  const trail: RowAudit[] = [
    { dateTime: '2026-06-04T06:30:00', userName: 'alice', actionType: 'UPDATE', actionDesc: 'Title, ScheduleOn' },
    { dateTime: '2026-06-02T01:15:00', userName: 'bob', actionType: 'UPDATE', actionDesc: 'PublishStatus_pkid' },
    { dateTime: '2026-06-01T00:00:00', userName: 'system', actionType: 'INSERT', actionDesc: 'Azure Fundamentals' }
  ];

  /** The local-time rendering of an audit timestamp, as the DatePipe in the badge will print it. */
  function local(dateTime: string, pattern: 'minute' | 'second'): string {
    const d = new Date(toUtcIso(dateTime));
    const pad = (n: number) => String(n).padStart(2, '0');
    const base = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    return pattern === 'minute' ? base : `${base}:${pad(d.getSeconds())}`;
  }

  function expectRequest(tableName = 'Course', pkid = '123'): TestRequest {
    const req = http.expectOne(r => r.url === url);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('tableName')).toBe(tableName);
    expect(req.request.params.get('pkid')).toBe(pkid);
    return req;
  }

  function badge(): HTMLElement {
    return (fixture.nativeElement as HTMLElement).querySelector('.row-audit-badge') as HTMLElement;
  }

  function dialog(): HTMLElement | null {
    return document.querySelector('.row-audit-dialog');
  }

  function openDialog(): void {
    badge().click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    fixture.destroy();
  });

  it('fetches the record history from /row-audits?tableName=&pkid= on load', () => {
    expectRequest('Course', '123').flush([]);
  });

  it('shows the bilingual label and the latest record inline without any click', () => {
    expectRequest().flush(trail);
    fixture.detectChanges();

    const text = badge().textContent?.replace(/\s+/g, ' ') ?? '';
    expect(text).toContain('異動紀錄 History');
    expect(text).toContain(`Update by alice · ${local(trail[0].dateTime, 'minute')}`);
    expect(text).not.toContain('bob');
    expect(dialog()).toBeNull();
  });

  it('renders a neutral "no history" state inline when the trail is empty', () => {
    expectRequest().flush([]);
    fixture.detectChanges();

    const text = badge().textContent?.replace(/\s+/g, ' ') ?? '';
    expect(text).toContain('異動紀錄 History');
    expect(text).toContain('尚無異動紀錄 No history');
    expect(badge().querySelector('.row-audit-badge__latest--muted')).not.toBeNull();
  });

  it('opens a dialog listing the full trail, newest first, with time / user / action / description', () => {
    expectRequest().flush(trail);
    fixture.detectChanges();

    openDialog();

    const dlg = dialog();
    expect(dlg).not.toBeNull();
    expect(dlg!.textContent).toContain('異動紀錄 History');
    expect(dlg!.textContent).toContain('Course · 123');

    const rows = Array.from(dlg!.querySelectorAll('tbody tr'));
    expect(rows.length).toBe(3);
    const cells = (row: Element) => Array.from(row.querySelectorAll('td')).map(td => td.textContent?.trim() ?? '');
    expect(cells(rows[0])).toEqual([local(trail[0].dateTime, 'second'), 'alice', 'Update', 'Title, ScheduleOn']);
    expect(cells(rows[1])).toEqual([local(trail[1].dateTime, 'second'), 'bob', 'Update', 'PublishStatus_pkid']);
    expect(cells(rows[2])).toEqual([local(trail[2].dateTime, 'second'), 'system', 'Insert', 'Azure Fundamentals']);

    const headers = Array.from(dlg!.querySelectorAll('thead th')).map(th => th.textContent?.trim());
    expect(headers).toEqual(['時間 DateTime', '使用者 UserName', '動作 ActionType', '說明 ActionDesc']);
  });

  it('shows a dash for an entry without a description', () => {
    expectRequest().flush([{ dateTime: '2026-06-01T00:00:00', userName: 'system', actionType: 'DELETE', actionDesc: null }]);
    fixture.detectChanges();

    openDialog();

    const cells = Array.from(dialog()!.querySelectorAll('tbody td')).map(td => td.textContent?.trim());
    expect(cells).toEqual([local('2026-06-01T00:00:00', 'second'), 'system', 'Delete', '—']);
  });

  it('opens the dialog with a friendly "no history yet" state when the trail is empty', () => {
    expectRequest().flush([]);
    fixture.detectChanges();

    openDialog();

    const dlg = dialog();
    expect(dlg).not.toBeNull();
    expect(dlg!.textContent).toContain('尚無異動紀錄 No history yet');
    expect(dlg!.querySelector('table')).toBeNull();
  });

  it('does not re-fetch when the dialog opens — the trail loaded on load is what it lists', () => {
    expectRequest().flush(trail);
    fixture.detectChanges();

    openDialog();

    http.expectNone(r => r.url === url);
  });

  it('shows an "unavailable" state when the request fails', () => {
    expectRequest().flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(badge().textContent).toContain('無法載入 Unavailable');
    openDialog();
    expect(dialog()!.textContent).toContain('無法載入異動紀錄 History could not be loaded.');
  });

  it('sends a string key unchanged (AppRole / AppUser)', () => {
    expectRequest('Course', '123').flush([]);
    fixture.componentInstance.tableName.set('AppUser');
    fixture.componentInstance.pkid.set('helen');
    fixture.detectChanges();

    expectRequest('AppUser', 'helen').flush([]);
  });

  it('renders nothing and requests nothing while there is no pkid (a new record)', () => {
    expectRequest().flush(trail);
    fixture.componentInstance.pkid.set(null);
    fixture.detectChanges();

    expect(badge()).toBeNull();
    http.expectNone(r => r.url === url);
  });

  it('reloads and closes the dialog when the record changes', async () => {
    expectRequest('Course', '123').flush(trail);
    fixture.detectChanges();
    openDialog();
    expect(dialog()).not.toBeNull();

    fixture.componentInstance.pkid.set(456);
    fixture.detectChanges();
    expectRequest('Course', '456').flush([]);
    fixture.detectChanges();
    // The dialog leaves through a (noop) animation; let it finish before looking for the element.
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(dialog()).toBeNull();
    expect(badge().textContent).toContain('尚無異動紀錄 No history');
  });
});

describe('toUtcIso', () => {
  it('marks an offset-less SQL datetime as UTC', () => {
    expect(toUtcIso('2026-06-04T06:30:00')).toBe('2026-06-04T06:30:00Z');
  });

  it('leaves a value that already carries an offset alone', () => {
    expect(toUtcIso('2026-06-04T06:30:00Z')).toBe('2026-06-04T06:30:00Z');
    expect(toUtcIso('2026-06-04T06:30:00+08:00')).toBe('2026-06-04T06:30:00+08:00');
  });
});
