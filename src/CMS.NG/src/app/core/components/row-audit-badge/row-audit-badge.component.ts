import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { RowAudit } from '@core/models/row-audit.model';
import { RowAuditService } from '@core/services/row-audit.service';

/**
 * Small "last changed by / when" tag for detail and edit toolbars.
 * Renders nothing until a pk is supplied and at least one audit row exists.
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [DatePipe, TagModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (latest(); as audit) {
      <p-tag
        class="row-audit-badge"
        severity="secondary"
        icon="pi pi-history"
        [pTooltip]="audit.actionDesc ?? ''"
        tooltipPosition="bottom"
        [value]="'最後異動 ' + audit.userName + ' ' + (audit.dateTime + 'Z' | date:'yyyy-MM-dd HH:mm')" />
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; }
    .row-audit-badge { font-weight: 400; }
  `
})
export class RowAuditBadgeComponent {
  private readonly rowAuditService = inject(RowAuditService);

  readonly tableName = input.required<string>();
  readonly pk = input<string | number | null | undefined>(null);

  protected readonly latest = signal<RowAudit | null>(null);

  constructor() {
    effect(() => {
      const table = this.tableName();
      const pk = this.pk();
      untracked(() => this.load(table, pk));
    });
  }

  private load(table: string, pk: string | number | null | undefined): void {
    if (pk === null || pk === undefined || pk === '') {
      this.latest.set(null);
      return;
    }
    this.rowAuditService.getForRow(table, pk, 1).subscribe({
      next: rows => this.latest.set(rows[0] ?? null),
      error: () => this.latest.set(null)
    });
  }
}
