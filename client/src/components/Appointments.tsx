import type { AppointmentRequest } from '../api';
import { formatDateTime, formatTime } from '../shared/date';

function confirmationStatus(appointment: AppointmentRequest) {
  if (appointment.status === 'PendingConfirmation') return `Čeká na potvrzení e-mailu${appointment.holdExpiresAt ? ' do ' + formatTime(appointment.holdExpiresAt) : ''}`;
  if (appointment.status === 'Confirmed') return 'Potvrzeno';
  if (appointment.status === 'Cancelled') return 'Zrušeno nebo vypršelo';
  return 'Ke zpracování';
}

export function AppointmentRow({ appointment, detailed = false }: { appointment: AppointmentRequest; detailed?: boolean }) {
  return (
    <article className={detailed ? 'cmsAppointmentRow detailed' : 'cmsAppointmentRow'}>
      <div><strong>{appointment.name}</strong><span>{appointment.petName} · {appointment.petType}</span></div>
      <div><span>{appointment.phone}</span>{appointment.email && <span>{appointment.email}</span>}</div>
      <div>{appointment.requestedStartAt ? <span>{formatDateTime(appointment.requestedStartAt)}</span> : <span>Bez termínu</span>}<small>{appointment.visitReason ?? appointment.status ?? 'New'} · {appointment.durationMinutes ?? 30} min</small></div>
      {detailed && <p>{appointment.message || 'Bez poznámky.'}</p>}
      <small>{confirmationStatus(appointment)}</small>
    </article>
  );
}

export function CalendarAppointmentCard({ appointment }: { appointment: AppointmentRequest }) {
  return (
    <article className="cmsCalendarAppointment">
      <time>{appointment.requestedStartAt ? formatTime(appointment.requestedStartAt) : 'Bez času'}{appointment.requestedEndAt ? ` - ${formatTime(appointment.requestedEndAt)}` : ''}</time>
      <strong>{appointment.petName || 'Pacient'}</strong>
      <span>{appointment.name} · {appointment.petType}</span>
      <small>{appointment.visitReason} · {appointment.durationMinutes} min</small>
      <small>{confirmationStatus(appointment)}</small>
      {appointment.message && <p>{appointment.message}</p>}
    </article>
  );
}
