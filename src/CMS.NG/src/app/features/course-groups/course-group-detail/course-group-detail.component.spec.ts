import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';
import { RowAuditService } from '@core/services/row-audit.service';
import { CourseGroupDetailComponent } from './course-group-detail.component';

describe('CourseGroupDetailComponent', () => {
  let service: jasmine.SpyObj<CourseGroupService>;
  let rowAudits: jasmine.SpyObj<RowAuditService>;

  const item: CourseGroup = { pkid: 1, description: '雲端運算' };

  async function setup(id = '1'): Promise<ComponentFixture<CourseGroupDetailComponent>> {
    await TestBed.configureTestingModule({
      imports: [CourseGroupDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseGroupService, useValue: service },
        { provide: RowAuditService, useValue: rowAudits },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id }) } } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CourseGroupDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById', 'delete']);
    service.getById.and.returnValue(of(item));
    service.delete.and.returnValue(of(void 0));

    rowAudits = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRow']);
    rowAudits.getForRow.and.returnValue(of([
      { pkid: 9, tableName: 'CourseGroup', userName: 'system', primaryKeyValues: '1', actionType: 'UPDATE', actionDesc: 'Description', dateTime: '2026-09-07T01:02:03' }
    ]));
  });

  it('loads the record from the :id route param', async () => {
    await setup('1');
    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders the two fields', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('主代碼');
    expect(text).toContain('說明');
    expect(text).toContain('雲端運算');
  });

  it('renders both primary-foreign link buttons', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('查看課程');
    expect(text).toContain('查看夥伴課程群組');
  });

  it('shows the row-audit badge for the record', async () => {
    const fixture = await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowAudits.getForRow).toHaveBeenCalledWith('CourseGroup', 1, 1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('最後異動 system');
  });

  it('shows a not-found message on 404', async () => {
    service.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = await setup('99');

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('找不到主代碼 99');
  });

  it('asks for confirmation with pkid and description before deleting', async () => {
    const fixture = await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const confirmation = TestBed.inject(ConfirmationService);
    spyOn(confirmation, 'confirm').and.callFake(options => {
      expect(options.message).toContain('<b>1</b>');
      expect(options.message).toContain('「雲端運算」');
      options.accept?.();
      return confirmation;
    });

    (fixture.componentInstance as unknown as { confirmDelete: () => void }).confirmDelete();

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(navigate).toHaveBeenCalledWith(['/course/course-groups']);
  });
});
