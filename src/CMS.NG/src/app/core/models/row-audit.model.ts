/**
 * One entry of a record's audit trail as returned by `GET /api/row-audits?tableName=&pkid=`.
 * `dateTime` is UTC without a 'Z' suffix (SQL `datetime`); `toUtcIso` in the badge appends it before formatting.
 */
export interface RowAudit {
  dateTime: string;
  userName: string;
  actionType: 'INSERT' | 'UPDATE' | 'DELETE' | string;
  actionDesc: string | null;
}
