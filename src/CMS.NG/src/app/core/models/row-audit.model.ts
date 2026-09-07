/** Row of dbo.RowAudit as returned by /api/row-audits/{table}/{pk}. `dateTime` is UTC without a 'Z' suffix. */
export interface RowAudit {
  pkid: number;
  tableName: string;
  userName: string;
  primaryKeyValues: string;
  actionType: 'INSERT' | 'UPDATE' | 'DELETE' | string;
  actionDesc: string | null;
  dateTime: string;
}
