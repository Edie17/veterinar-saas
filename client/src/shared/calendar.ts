type CalendarVisit = { id: number; startAt: string; endAt: string; petName: string; visitReason?: string | null };
const escapeText = (value: string) => value.replace(/\\/g, '\\\\').replace(/\r\n|\r|\n/g, '\\n').replace(/;/g, '\\;').replace(/,/g, '\\,');
const stamp = (value: string) => new Date(value).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
function fold(line: string) {
  const encoder = new TextEncoder(); let width = 0; let result = '';
  for (const character of line) {
    const size = encoder.encode(character).length;
    if (width + size > 75) { result += '\r\n '; width = 1; }
    result += character; width += size;
  }
  return result;
}
export function visitCalendar(visit: CalendarVisit, clinicName: string, address: string, now = new Date()) {
  return [
    'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Veterinar//Rezervace//CS', 'CALSCALE:GREGORIAN', 'BEGIN:VEVENT',
    `UID:veterinar-${visit.id}@${window.location.hostname}`, `DTSTAMP:${stamp(now.toISOString())}`,
    `DTSTART:${stamp(visit.startAt)}`, `DTEND:${stamp(visit.endAt)}`,
    `SUMMARY:${escapeText(`${clinicName} – ${visit.petName}`)}`, `LOCATION:${escapeText(address)}`,
    `DESCRIPTION:${escapeText(`${visit.visitReason ?? 'Návštěva ordinace'}. Pro změnu termínu použijte soukromý odkaz v e-mailu. Po přesunutí aktualizujte událost v kalendáři.`)}`,
    'BEGIN:VALARM', 'TRIGGER:-PT1H', 'ACTION:DISPLAY', 'DESCRIPTION:Připomenutí návštěvy veterinární ordinace',
    'END:VALARM', 'END:VEVENT', 'END:VCALENDAR', '',
  ].map(fold).join('\r\n');
}
export function downloadCalendar(content: string) {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/calendar;charset=utf-8' }));
  const link = document.createElement('a'); link.href = url; link.download = 'veterinar-rezervace.ics';
  document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
