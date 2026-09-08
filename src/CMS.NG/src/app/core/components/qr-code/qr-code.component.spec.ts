import { Component, signal, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { QrCodeService } from '@core/services/qr-code.service';
import { QrCodeComponent } from './qr-code.component';

@Component({
  imports: [QrCodeComponent],
  template: `<app-qr-code [text]="text()" [title]="title()" [fileName]="fileName()" [size]="120" />`
})
class HostComponent {
  readonly text = signal('https://example.test/a/1');
  readonly title = signal('ABC-1');
  readonly fileName = signal<string | null>(null);
  readonly qr = viewChild.required(QrCodeComponent);
}

/** Counts pixels darker than mid-grey on the canvas (QR modules are painted black). */
function darkPixels(canvas: HTMLCanvasElement): number {
  const ctx = canvas.getContext('2d')!;
  const { data } = ctx.getImageData(0, 0, canvas.width, canvas.height);
  let dark = 0;
  for (let i = 0; i < data.length; i += 4) {
    if (data[i] < 128 && data[i + 3] > 0) {
      dark++;
    }
  }
  return dark;
}

describe('QrCodeComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let qrService: QrCodeService;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();

    qrService = TestBed.inject(QrCodeService);
    spyOn(qrService, 'toCanvas').and.callThrough();

    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await settle();
  });

  it('encodes the text input onto the canvas', () => {
    const host = fixture.nativeElement as HTMLElement;
    const canvas = host.querySelector<HTMLCanvasElement>('canvas.qr-code__canvas')!;

    expect(qrService.toCanvas).toHaveBeenCalledWith(canvas, 'https://example.test/a/1', jasmine.objectContaining({ width: 120 }));
    expect(host.querySelector('figure.qr-code')?.getAttribute('data-qr-text')).toBe('https://example.test/a/1');
    expect(canvas.width).toBe(120);
    const dark = darkPixels(canvas);
    expect(dark).toBeGreaterThan(0);
    expect(dark).toBeLessThan(120 * 120);
  });

  it('shows the title as the caption', () => {
    const caption = (fixture.nativeElement as HTMLElement).querySelector('figcaption.qr-code__title');
    expect(caption?.textContent?.trim()).toBe('ABC-1');
  });

  it('re-encodes when the text changes', async () => {
    fixture.componentInstance.text.set('https://example.test/a/2');
    fixture.detectChanges();
    await settle();

    expect(qrService.toCanvas).toHaveBeenCalledTimes(2);
    expect(qrService.toCanvas).toHaveBeenCalledWith(jasmine.any(HTMLCanvasElement), 'https://example.test/a/2', jasmine.anything());
  });

  it('builds a PNG data URL taller than the symbol (title strip below it)', () => {
    const dataUrl = fixture.componentInstance.qr().toDataUrl();
    expect(dataUrl.startsWith('data:image/png;base64,')).toBeTrue();
    expect(dataUrl.length).toBeGreaterThan(100);
  });

  it('downloads the image named after the title by default', () => {
    let clicked: HTMLAnchorElement | undefined;
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
      clicked = this;
    });

    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.qr-code__download button')!;
    expect(button.disabled).toBeFalse();
    button.click();

    expect(clicked).toBeDefined();
    expect(clicked!.download).toBe('ABC-1.png');
    expect(clicked!.href.startsWith('data:image/png;base64,')).toBeTrue();
  });

  it('uses the explicit fileName input when given', async () => {
    fixture.componentInstance.fileName.set('custom-name.png');
    fixture.detectChanges();
    await settle();

    let clicked: HTMLAnchorElement | undefined;
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
      clicked = this;
    });
    fixture.componentInstance.qr().download();

    expect(clicked!.download).toBe('custom-name.png');
  });

  it('shows an error and keeps the download disabled when encoding fails', async () => {
    (qrService.toCanvas as jasmine.Spy).and.rejectWith(new Error('too long'));
    fixture.componentInstance.text.set('x'.repeat(5000));
    fixture.detectChanges();
    await settle();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('無法產生 QR Code');
    expect(host.querySelector<HTMLButtonElement>('.qr-code__download button')!.disabled).toBeTrue();
  });
});
