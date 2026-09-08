import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { RowAudit } from '@core/models/row-audit.model';
import { RowAuditService } from '@core/services/row-audit.service';

/** RowAudit.ActionType → the word shown on the badge and in the dialog. Unknown types are shown as stored. */
export const ROW_AUDIT_ACTION_LABELS: Readonly<Record<string, string>> = {
  INSERT: 'Insert',
  UPDATE: 'Update',
  DELETE: 'Delete'
};

/** SQL `datetime` comes back without an offset; it is UTC, so mark it before `Date` parses it as local time. */
export function toUtcIso(dateTime: string): string {
  return /(?:Z|[+-]\d{2}:\d{2})$/.test(dateTime) ? dateTime : `${dateTime}Z`;
}

/**
 * 異動紀錄 History — a small toolbar button for detail and edit pages. It loads the record's audit trail once
 * (`GET /api/row-audits?tableName=&pkid=`), shows the latest entry inline ("Update by alice · 2026-06-04 14:30",
 * or a neutral "no history" state) and opens a dialog with the full trail, newest first, on click.
 * Renders nothing until a `pkid` is supplied (a new record has no history to show).
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [DatePipe, ButtonModule, DialogModule, TableModule, TagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasRecord()) {
      <p-button
        styleClass="row-audit-badge"
        severity="secondary"
        [outlined]="true"
        size="small"
        type="button"
        ariaLabel="異動紀錄 History"
        (onClick)="open()">
        <i class="pi pi-history" aria-hidden="true"></i>
        <span class="row-audit-badge__label">異動紀錄 History</span>
        <span class="row-audit-badge__latest" [class.row-audit-badge__latest--muted]="!latest()">
          @if (loading()) {
            載入中…
          } @else if (failed()) {
            無法載入 Unavailable
          } @else if (latest(); as audit) {
            {{ actionLabel(audit.actionType) }} by {{ audit.userName }} · {{ toUtcIso(audit.dateTime) | date:'yyyy-MM-dd HH:mm' }}
          } @else {
            尚無異動紀錄 No history
          }
        </span>
      </p-button>

      <p-dialog
        header="異動紀錄 History"
        [(visible)]="dialogVisible"
        [modal]="true"
        [dismissableMask]="true"
        [draggable]="false"
        styleClass="row-audit-dialog"
        [style]="{ width: 'min(64rem, 95vw)' }">
        <p class="row-audit-dialog__record">{{ tableName() }} · {{ pkid() }}</p>
        @if (failed()) {
          <p class="row-audit-dialog__empty">無法載入異動紀錄 History could not be loaded.</p>
        } @else if (rows().length === 0) {
          <p class="row-audit-dialog__empty">尚無異動紀錄 No history yet.</p>
        } @else {
          <p-table [value]="rows()" size="small" styleClass="row-audit-dialog__table">
            <ng-template #header>
              <tr>
                <th class="row-audit-dialog__when">時間 DateTime</th>
                <th>使用者 UserName</th>
                <th>動作 ActionType</th>
                <th>說明 ActionDesc</th>
              </tr>
            </ng-template>
            <ng-template #body let-row>
              <tr>
                <td class="row-audit-dialog__when">{{ toUtcIso(row.dateTime) | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ row.userName }}</td>
                <td><p-tag [value]="actionLabel(row.actionType)" [severity]="actionSeverity(row.actionType)" /></td>
                <td class="row-audit-dialog__desc">{{ row.actionDesc || '—' }}</td>
              </tr>
            </ng-template>
          </p-table>
        }
      </p-dialog>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; }
    :host ::ng-deep .row-audit-badge {
      gap: 0.5rem;
      font-weight: 400;
      white-space: nowrap;
    }
    .row-audit-badge__label { font-weight: 600; }
    .row-audit-badge__latest {
      padding-left: 0.5rem;
      border-left: 1px solid var(--p-content-border-color);
      color: var(--p-text-color);
    }
    .row-audit-badge__latest--muted { color: var(--p-text-muted-color); }
    .row-audit-dialog__record {
      margin: 0 0 0.75rem;
      color: var(--p-text-muted-color);
    }
    .row-audit-dialog__empty {
      margin: 0;
      padding: 1.5rem 0;
      text-align: center;
      color: var(--p-text-muted-color);
    }
    .row-audit-dialog__when { white-space: nowrap; }
    .row-audit-dialog__desc { word-break: break-word; }
  `
})
export class RowAuditBadgeComponent {
  private readonly rowAuditService = inject(RowAuditService);

  /** `RowAudit.TableName` of the page's entity, e.g. `Course`. */
  readonly tableName = input.required<string>();
  /** The record's key as the audit writer stored it: the numeric pkid, or `RoleId` / `UserId` for AppRole / AppUser. */
  readonly pkid = input<string | number | null | undefined>(null);

  protected readonly rows = signal<RowAudit[]>([]);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly dialogVisible = signal(false);

  /** The most recent entry — the endpoint returns newest first. */
  protected readonly latest = computed<RowAudit | null>(() => this.rows()[0] ?? null);
  protected readonly hasRecord = computed(() => {
    const pkid = this.pkid();
    return pkid !== null && pkid !== undefined && pkid !== '';
  });

  protected readonly toUtcIso = toUtcIso;

  constructor() {
    effect(() => {
      const table = this.tableName();
      const pkid = this.pkid();
      untracked(() => this.load(table, pkid));
    });
  }

  protected open(): void {
    this.dialogVisible.set(true);
  }

  protected actionLabel(actionType: string): string {
    return ROW_AUDIT_ACTION_LABELS[actionType] ?? actionType;
  }

  protected actionSeverity(actionType: string): 'success' | 'info' | 'danger' | 'secondary' {
    switch (actionType) {
      case 'INSERT':
        return 'success';
      case 'UPDATE':
        return 'info';
      case 'DELETE':
        return 'danger';
      default:
        return 'secondary';
    }
  }

  private load(table: string, pkid: string | number | null | undefined): void {
    this.dialogVisible.set(false);
    this.rows.set([]);
    this.failed.set(false);

    if (pkid === null || pkid === undefined || pkid === '') {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.rowAuditService.getForRecord(table, pkid).subscribe({
      next: rows => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      }
    });
  }
}
