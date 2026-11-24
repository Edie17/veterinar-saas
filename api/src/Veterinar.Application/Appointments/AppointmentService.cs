using Veterinar.Application.Mapping;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Scheduling;
using Veterinar.Domain.Appointments;

namespace Veterinar.Application.Appointments;

public sealed class AppointmentService(IAppointmentRepository appointments, IClinicContext clinicContext, SchedulingService scheduling, DtoMapper mapper, TimeProvider timeProvider, IBookingStore bookings, IBookingEmail emailSender)
{
    private static readonly Regex SuspiciousInputPattern = new(@"<[^>]*>|</|javascript\s*:|data\s*:\s*text/html|vbscript\s*:|on[a-z]+\s*=|&#x?[0-9a-f]+;", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
    private static readonly Regex ControlCharactersPattern = new(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^(\+420)?[0-9]{9}$|^\+[1-9][0-9]{7,14}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedPetTypes = ["Pes", "Kočka", "Drobné zvíře", "Jiné"];

    public Task<int> CreateAsync(CreateAppointmentRequest request, CancellationToken ct = default) => CreateInternalAsync(request, true, ct);
    public Task<int> CreateManualAsync(CreateAppointmentRequest request, CancellationToken ct = default) => CreateInternalAsync(request, false, ct);

    private async Task<int> CreateInternalAsync(CreateAppointmentRequest request, bool isPublic, CancellationToken ct)
    {
        var name = NormalizePlainText(request.Name, "Jméno", 2, 80);
        var phone = NormalizePhone(request.Phone);
        var email = NormalizeEmail(request.Email);
        var petName = NormalizePlainText(request.PetName, "Jméno mazlíčka", 1, 60);
        var petType = NormalizePlainText(request.PetType, "Druh zvířete", 1, 40);
        var visitReason = NormalizePlainText(request.VisitReason, "Důvod návštěvy", 1, 120);
        var message = NormalizeOptionalPlainText(request.Message, "Zpráva", 600);

        if (!AllowedPetTypes.Contains(petType))
        {
            throw new AppointmentReservationException("Vyberte prosím platný druh zvířete.");
        }

        var appointmentType = await appointments.FindPublishedTypeByTitleAsync(clinicContext.ClinicId, visitReason, ct);
        if (appointmentType is null)
        {
            throw new AppointmentReservationException("Vyberte prosím platný důvod návštěvy.");
        }

        if (request.RequestedStartAt is null || request.RequestedEndAt is null)
        {
            throw new AppointmentReservationException("Vyberte prosím volný termín rezervace.");
        }

        if (request.RequestedStartAt.Value >= request.RequestedEndAt.Value)
        {
            throw new AppointmentReservationException("Vybraný termín není platný.");
        }

        if (request.RequestedStartAt.Value <= timeProvider.GetUtcNow())
        {
            throw new AppointmentReservationException("Vybraný termín už proběhl. Vyberte prosím budoucí čas.");
        }

        var requestedDate = ClinicTime.Date(request.RequestedStartAt.Value);
        if (requestedDate > ClinicTime.Date(timeProvider.GetUtcNow()).AddDays(60)) throw new AppointmentReservationException("Rezervovat lze nejvýše 60 dní předem.");
        var expectedEnd = request.RequestedStartAt.Value.AddMinutes(appointmentType.DurationMinutes);
        if (request.RequestedEndAt.Value != expectedEnd)
        {
            throw new AppointmentReservationException("Vybraný termín neodpovídá délce návštěvy.");
        }

        var availableSlots = await scheduling.GetAvailableSlotsAsync(requestedDate, appointmentType.DurationMinutes, ct);
        var slot = availableSlots.SingleOrDefault(x => x.StartAt == request.RequestedStartAt.Value && x.EndAt == request.RequestedEndAt.Value);
        if (slot is null || !slot.IsAvailable)
        {
            throw new AppointmentReservationException("Vybraný termín už není dostupný. Vyberte prosím jiný čas.");
        }

        var now = timeProvider.GetUtcNow();
        var token = isPublic ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : null;
        var managementToken = isPublic ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : null;
        var entity = new AppointmentRequest
        {
            ClinicId = clinicContext.ClinicId,
            Name = name,
            Phone = phone,
            Email = email,
            PetName = petName,
            PetType = petType,
            VisitReason = appointmentType.Title,
            DurationMinutes = appointmentType.DurationMinutes,
            Message = message,
            RequestedStartAt = request.RequestedStartAt,
            RequestedEndAt = request.RequestedEndAt,
            Status = isPublic ? AppointmentStatus.PendingConfirmation : AppointmentStatus.Confirmed,
            CreatedAt = now,
            CreatedUtcSeconds = now.ToUnixTimeSeconds(),
            StartUtcSeconds = request.RequestedStartAt.Value.ToUnixTimeSeconds(),
            EndUtcSeconds = request.RequestedEndAt.Value.ToUnixTimeSeconds(),
            HoldExpiresUtcSeconds = isPublic ? now.AddMinutes(15).ToUnixTimeSeconds() : null,
            ConfirmationTokenHash = token is null ? null : HashToken(token),
            ManagementTokenHash = managementToken is null ? null : HashToken(managementToken),
            IsPublicBooking = isPublic
        };
        await bookings.ReserveAsync(entity, now.ToUnixTimeSeconds(), ct);
        if (isPublic)
        {
            try { await emailSender.SendConfirmationAsync(email, token!, now.AddMinutes(15), ct, managementToken); }
            catch
            {
                await bookings.CancelAsync(clinicContext.ClinicId, entity.Id, CancellationToken.None);
                throw;
            }
        }
        return entity.Id;
    }

    public Task ConfirmAsync(string token, CancellationToken ct = default)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new Common.RequestException("Odkaz je neplatný.");
        return bookings.ConfirmAsync(clinicContext.ClinicId, HashToken(token), timeProvider.GetUtcNow().ToUnixTimeSeconds(), ct);
    }
    public Task CancelAsync(int id, CancellationToken ct = default) => bookings.CancelAsync(clinicContext.ClinicId, id, ct);
    private static string ManagementHash(string token)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new Common.RequestException("Odkaz pro správu rezervace je neplatný nebo už není platný.", 404);
        return HashToken(token);
    }
    public async Task<ManagedAppointmentDto> GetManagedAsync(string token, CancellationToken ct = default)
    {
        var request = await bookings.FindManagedAsync(clinicContext.ClinicId, ManagementHash(token), ct);
        if (request is null || request.Status == AppointmentStatus.Cancelled || request.StartUtcSeconds <= timeProvider.GetUtcNow().ToUnixTimeSeconds())
            throw new Common.RequestException("Odkaz pro správu rezervace je neplatný nebo už není platný.", 404);
        if (request.Status != AppointmentStatus.Confirmed)
            throw new Common.RequestException("Nejdříve potvrďte rezervaci pomocí potvrzovacího odkazu v e-mailu.", 409);
        return new(request.Id, request.PetName, request.VisitReason, request.DurationMinutes, request.RequestedStartAt, request.RequestedEndAt, request.Status.ToString());
    }
    public Task CancelManagedAsync(string token, CancellationToken ct = default) =>
        bookings.CancelManagedAsync(clinicContext.ClinicId, ManagementHash(token), timeProvider.GetUtcNow().ToUnixTimeSeconds(), ct);
    public async Task<ManagedAppointmentDto> MoveManagedAsync(string token, DateTimeOffset start, CancellationToken ct = default)
    {
        var current = await GetManagedAsync(token, ct);
        if (current.StartAt == start) return current;
        var slots = await scheduling.GetAvailableSlotsAsync(ClinicTime.Date(start), current.DurationMinutes, ct);
        var slot = slots.SingleOrDefault(x => x.StartAt == start && x.IsAvailable);
        if (slot is null) throw new Common.RequestException("Vybraný termín už není dostupný. Původní rezervace zůstává platná.", 409);
        await bookings.MoveManagedAsync(clinicContext.ClinicId, ManagementHash(token), slot.StartAt, slot.EndAt, timeProvider.GetUtcNow().ToUnixTimeSeconds(), ct);
        return await GetManagedAsync(token, ct);
    }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void CheckRawLength(string? value, string label, int maxLength)
    {
        if (value?.Length > maxLength) throw new AppointmentReservationException($"{label} je příliš dlouhé.");
    }

    private static string NormalizePlainText(string? value, string label, int minLength, int maxLength)
    {
        CheckRawLength(value, label, maxLength);
        var normalized = NormalizeText(value);
        if (normalized.Length < minLength)
        {
            throw new AppointmentReservationException($"{label} je příliš krátké.");
        }

        if (normalized.Length > maxLength)
        {
            throw new AppointmentReservationException($"{label} je příliš dlouhé.");
        }

        RejectSuspiciousInput(normalized, label);
        return normalized;
    }

    private static string? NormalizeOptionalPlainText(string? value, string label, int maxLength)
    {
        CheckRawLength(value, label, maxLength);
        var normalized = NormalizeText(value);
        if (normalized.Length == 0) return null;
        if (normalized.Length > maxLength)
        {
            throw new AppointmentReservationException($"{label} je příliš dlouhá.");
        }

        RejectSuspiciousInput(normalized, label);
        return normalized;
    }

    private static string NormalizePhone(string? value)
    {
        CheckRawLength(value, "Telefon", 40);
        var normalized = NormalizeText(value).Replace("(", " ").Replace(")", " ").Replace(".", " ").Replace("-", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        RejectSuspiciousInput(normalized, "Telefon");

        var compact = normalized.Replace(" ", "");
        if (!PhonePattern.IsMatch(compact))
        {
            throw new AppointmentReservationException("Zadejte prosím platné telefonní číslo.");
        }

        return compact.Length == 9 ? "+420" + compact : compact;
    }

    private static string NormalizeEmail(string? value)
    {
        var normalized = NormalizePlainText(value, "E-mail", 3, 120);
        try
        {
            var address = new MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppointmentReservationException("Zadejte prosím platný e-mail.");
            }
        }
        catch (FormatException)
        {
            throw new AppointmentReservationException("Zadejte prosím platný e-mail.");
        }

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var withoutControlCharacters = ControlCharactersPattern.Replace(value, string.Empty);
        return Regex.Replace(withoutControlCharacters, @"\s+", " ").Trim();
    }

    private static void RejectSuspiciousInput(string value, string label)
    {
        if (SuspiciousInputPattern.IsMatch(value))
        {
            throw new AppointmentReservationException($"{label} nesmí obsahovat HTML, skripty ani vložený kód.");
        }
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetLatestAsync(int take = 1000, CancellationToken ct = default)
    {
        var latest = await appointments.GetLatestAsync(clinicContext.ClinicId, take, ct);
        // Read-only projection: expiry must be reflected even when no subsequent write has run.
        foreach (var request in latest)
            if (request.Status == AppointmentStatus.PendingConfirmation && request.HoldExpiresUtcSeconds <= timeProvider.GetUtcNow().ToUnixTimeSeconds())
                request.Status = AppointmentStatus.Cancelled;
        return mapper.Map<IReadOnlyList<AppointmentDto>>(latest);
    }

    public async Task<IReadOnlyList<AppointmentTypeDto>> GetAppointmentTypesAsync(bool publicOnly = false, CancellationToken ct = default)
    {
        var types = await appointments.GetTypesAsync(clinicContext.ClinicId, publicOnly, ct);
        return mapper.Map<IReadOnlyList<AppointmentTypeDto>>(types);
    }

    public async Task<AppointmentTypeDto> UpsertAppointmentTypeAsync(AppointmentTypeInput input, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        AppointmentType entity;
        if (input.Id is { } id)
        {
            entity = await appointments.FindTypeAsync(clinicId, id, ct) ?? throw new Veterinar.Application.Common.RequestException("Appointment type was not found.");
        }
        else
        {
            entity = new AppointmentType { ClinicId = clinicId, Title = input.Title.Trim(), DurationMinutes = input.DurationMinutes, SortOrder = input.SortOrder, IsPublished = input.IsPublished };
            appointments.Add(entity);
        }

        entity.Title = input.Title.Trim();
        entity.DurationMinutes = input.DurationMinutes;
        entity.SortOrder = input.SortOrder;
        entity.IsPublished = input.IsPublished;
        Common.ValidationRules.Check(entity.Validate);
        await appointments.SaveChangesAsync(ct);
        return mapper.Map<AppointmentTypeDto>(entity);
    }

    public async Task DeleteAppointmentTypeAsync(int id, CancellationToken ct = default)
    {
        var entity = await appointments.FindTypeAsync(clinicContext.ClinicId, id, ct);
        if (entity is null) return;
        appointments.Remove(entity);
        await appointments.SaveChangesAsync(ct);
    }

}
