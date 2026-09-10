/**
 * Small, exception-safe wrappers around sessionStorage used by list pages to remember
 * filters / sort / paging between navigations. Storage can be unavailable or throw
 * (private mode, quota), so every call is guarded.
 */
export function readSession<T>(key: string, fallback: T): T {
  try {
    const raw = sessionStorage.getItem(key);
    return raw === null ? fallback : (JSON.parse(raw) as T);
  } catch {
    return fallback;
  }
}

export function writeSession(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(key, JSON.stringify(value));
  } catch {
    // ignore – remembering UI state is best-effort
  }
}

export function removeSession(key: string): void {
  try {
    sessionStorage.removeItem(key);
  } catch {
    // ignore
  }
}
