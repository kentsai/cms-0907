import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { QrCodeService } from '@core/services/qr-code.service';

/**
 * Renders `text` as a QR code with `title` as its caption and offers a PNG download.
 * The downloaded image is composited on a white background with the title printed
 * beneath the symbol, so a printed/shared copy still identifies what it points to.
 */
@Component({
  selector: 'app-qr-code',
  imports: [ButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <figure class="qr-code" [attr.data-qr-text]="text()">
      <canvas #canvas class="qr-code__canvas" role="img" [attr.aria-label]="'QR Code ' + title()"></canvas>
      <figcaption class="qr-code__title">{{ title() }}</figcaption>
      @if (error()) {
        <span class="qr-code__error">無法產生 QR Code</span>
      }
      @if (showDownload()) {
        <p-button
          class="qr-code__download"
          label="下載 QR Code"
          icon="pi pi-download"
          severity="secondary"
          size="small"
          [outlined]="true"
          [disabled]="!ready()"
          (onClick)="download()" />
      }
    </figure>
  `,
  styles: `
    :host { display: inline-block; }
    .qr-code {
      display: inline-flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      padding: 0.75rem;
      border: 1px solid var(--p-content-border-color);
      border-radius: var(--p-border-radius-md);
      background: #fff;
    }
    .qr-code__canvas { display: block; }
    .qr-code__title {
      font-family: var(--p-font-family, monospace);
      font-weight: 600;
      color: #000;
      word-break: break-all;
      text-align: center;
    }
    .qr-code__error { color: var(--p-red-500); font-size: 0.875rem; }
  `
})
export class QrCodeComponent {
  private readonly qr = inject(QrCodeService);

  /** Content to encode (typically a URL). */
  readonly text = input.required<string>();
  /** Caption shown under the symbol and baked into the downloaded image. */
  readonly title = input.required<string>();
  /** Download file name; defaults to `<title>.png`. */
  readonly fileName = input<string | null>(null);
  /** Edge length of the on-screen symbol in pixels. */
  readonly size = input(160);
  /** Whether the 下載 QR Code button is rendered (off for customer-facing views such as the print page). */
  readonly showDownload = input(true);
  /** Emits once per render, after the symbol is drawn (`'ready'`) or encoding failed (`'error'`). */
  readonly settled = output<'ready' | 'error'>();

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  protected readonly ready = signal(false);
  protected readonly error = signal(false);

  /** Rendering is async; an emit after the component is gone would be an error (NG0953). */
  private destroyed = false;

  constructor() {
    inject(DestroyRef).onDestroy(() => (this.destroyed = true));
    effect(() => {
      const canvas = this.canvas().nativeElement;
      const text = this.text();
      const size = this.size();
      untracked(() => void this.render(canvas, text, size));
    });
  }

  /** Builds the downloadable PNG: white background, the QR symbol, and the title below it. */
  toDataUrl(): string {
    const src = this.canvas().nativeElement;
    const padding = 16;
    const titleHeight = 32;
    const out = document.createElement('canvas');
    out.width = src.width + padding * 2;
    out.height = src.height + padding * 2 + titleHeight;

    const ctx = out.getContext('2d');
    if (!ctx) {
      throw new Error('2D canvas context unavailable');
    }
    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, out.width, out.height);
    ctx.drawImage(src, padding, padding);
    ctx.fillStyle = '#000';
    ctx.font = '600 18px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(this.title(), out.width / 2, padding + src.height + titleHeight / 2, out.width - padding * 2);

    return out.toDataURL('image/png');
  }

  /** Triggers a browser download of {@link toDataUrl} as `fileName`. */
  download(): void {
    if (!this.ready()) {
      return;
    }
    const link = document.createElement('a');
    link.href = this.toDataUrl();
    link.download = this.fileName() ?? `${this.title()}.png`;
    link.click();
  }

  private async render(canvas: HTMLCanvasElement, text: string, size: number): Promise<void> {
    this.ready.set(false);
    this.error.set(false);
    let outcome: 'ready' | 'error';
    try {
      await this.qr.toCanvas(canvas, text, { width: size });
      this.ready.set(true);
      outcome = 'ready';
    } catch {
      this.error.set(true);
      outcome = 'error';
    }
    if (!this.destroyed) {
      this.settled.emit(outcome);
    }
  }
}
