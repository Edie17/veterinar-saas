import type { WorkingHoursRule } from '../api';
import { dayNames } from '../shared/constants';

export function HoursTable({ rules, compact = false }: { rules: WorkingHoursRule[]; compact?: boolean }) {
  return (
    <div className={compact ? 'hoursTable compact' : 'hoursTable'}>
      {rules.map(rule => (
        <div key={rule.dayOfWeek}>
          <strong>{dayNames[rule.dayOfWeek]}</strong>
          <span>{rule.isClosed ? 'Zavřeno' : `${rule.opensAt} - ${rule.closesAt}`}</span>
        </div>
      ))}
    </div>
  );
}
