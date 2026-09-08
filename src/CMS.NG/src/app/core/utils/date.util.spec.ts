import { addDays, addYears, formatMonthDay, formatMonthDayWeekday, fromIso, startOfWeek, toIso } from './date.util';

describe('date.util', () => {
  it('toIso uses local date components and zero-pads', () => {
    expect(toIso(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(toIso(new Date(2026, 11, 31, 23, 59))).toBe('2026-12-31');
  });

  it('toIso returns null for null or invalid dates', () => {
    expect(toIso(null)).toBeNull();
    expect(toIso(undefined)).toBeNull();
    expect(toIso(new Date('not a date'))).toBeNull();
  });

  it('fromIso parses yyyy-MM-dd into a local-midnight Date', () => {
    const d = fromIso('2026-09-07')!;
    expect(d.getFullYear()).toBe(2026);
    expect(d.getMonth()).toBe(8);
    expect(d.getDate()).toBe(7);
    expect(d.getHours()).toBe(0);
  });

  it('fromIso tolerates a trailing time part and rejects garbage', () => {
    expect(fromIso('2026-09-07T00:00:00')?.getDate()).toBe(7);
    expect(fromIso('')).toBeNull();
    expect(fromIso(null)).toBeNull();
    expect(fromIso('07/09/2026')).toBeNull();
  });

  it('toIso and fromIso round-trip', () => {
    expect(toIso(fromIso('2030-02-28'))).toBe('2030-02-28');
  });

  it('addYears returns a new date and leaves the original untouched', () => {
    const start = new Date(2026, 2, 15);
    const later = addYears(start, 10);

    expect(later.getFullYear()).toBe(2036);
    expect(later.getMonth()).toBe(2);
    expect(later.getDate()).toBe(15);
    expect(start.getFullYear()).toBe(2026);
  });

  it('addDays shifts across month boundaries and returns local midnight', () => {
    const shifted = addDays(new Date(2026, 2, 30, 15, 30), 3);

    expect(toIso(shifted)).toBe('2026-04-02');
    expect(shifted.getHours()).toBe(0);
    expect(toIso(addDays(new Date(2026, 0, 1), -1))).toBe('2025-12-31');
  });

  it('startOfWeek snaps every day of the week to its Monday (Sunday goes back six days)', () => {
    // 2026-03-16 is a Monday.
    for (let day = 16; day <= 22; day++) {
      expect(toIso(startOfWeek(new Date(2026, 2, day)))).toBe('2026-03-16');
    }
    expect(toIso(startOfWeek(new Date(2026, 2, 23)))).toBe('2026-03-23');
  });

  it('formatMonthDay and formatMonthDayWeekday match the board headers', () => {
    expect(formatMonthDay(new Date(2026, 2, 16))).toBe('3/16');
    expect(formatMonthDayWeekday(new Date(2026, 2, 16))).toBe('3/16 (一)');
    expect(formatMonthDayWeekday(new Date(2026, 2, 22))).toBe('3/22 (日)');
  });
});
