/** Compact age for lists: "45m", "6h", "3d". Exact timestamps are in the detail view and tooltips. */
export function formatAge(isoTimestamp: string, now: number = Date.now()): string {
  const minutes = Math.max(0, Math.floor((now - Date.parse(isoTimestamp)) / 60_000));
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 48) return `${hours}h`;
  return `${Math.floor(hours / 24)}d`;
}

/** Open work older than this is flagged as ageing in the list. */
export const AGEING_DAYS = 7;

export function ageInDays(isoTimestamp: string, now: number = Date.now()): number {
  return (now - Date.parse(isoTimestamp)) / 86_400_000;
}
