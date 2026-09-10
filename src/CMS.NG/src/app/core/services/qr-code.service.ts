import { Injectable } from '@angular/core';
import { toCanvas } from 'qrcode';

/** Options accepted by {@link QrCodeService.toCanvas}. */
export interface QrCodeRenderOptions {
  /** Edge length of the rendered QR image in CSS pixels (the canvas is resized to match). */
  width: number;
  /** Quiet zone around the symbol, in modules. */
  margin?: number;
}

/**
 * Thin injectable wrapper around the `qrcode` library so components can be tested
 * without touching the real encoder (spy on `toCanvas`).
 */
@Injectable({ providedIn: 'root' })
export class QrCodeService {
  /** Encodes `text` as a QR symbol and paints it onto `canvas`. Rejects on unencodable input. */
  toCanvas(canvas: HTMLCanvasElement, text: string, options: QrCodeRenderOptions): Promise<void> {
    return toCanvas(canvas, text, {
      width: options.width,
      margin: options.margin ?? 2,
      errorCorrectionLevel: 'M'
    });
  }
}
