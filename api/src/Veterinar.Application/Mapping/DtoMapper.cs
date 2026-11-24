using Veterinar.Application.Appointments;
using Veterinar.Application.Auth;
using Veterinar.Application.Scheduling;
using Veterinar.Application.Site;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Users;

namespace Veterinar.Application.Mapping;

/// <summary>Explicit one-way mappings; confirmation secrets never enter a response DTO.</summary>
public sealed class DtoMapper
{
    public T Map<T>(object source)
    {
        object result = source switch
        {
            ClinicUser x => new AuthenticatedAdminDto(x.Email, x.Role.ToString(), x.ClinicId, x.Id,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(x.PasswordHash)))),
            PageContent x => new PageContentDto(x.Id, x.ClinicName, x.HeroTitle, x.HeroText, x.Phone, x.Email, x.Address, x.UpdatedAt),
            ServiceItem x => new ServiceItemDto(x.Id, x.Title, x.Description, x.Icon, x.SortOrder, x.IsPublished),
            Article x => new ArticleDto(x.Id, x.ArticleSectionId, x.Title, x.Slug, x.Summary, x.Body, x.SortOrder, x.IsPublished, x.PublishedAt, x.UpdatedAt),
            ArticleSection x => new ArticleSectionDto(x.Id, x.Title, x.Slug, x.Description, x.SortOrder, x.IsPublished, x.Articles.OrderBy(a => a.SortOrder).ThenByDescending(a => a.PublishedAt).Select(Map<ArticleDto>).ToList()),
            AppointmentRequest x => new AppointmentDto(x.Id, x.Name, x.Phone, x.Email, x.PetName, x.PetType, x.VisitReason, x.DurationMinutes, x.Message, x.Status.ToString(), x.CreatedAt, x.RequestedStartAt, x.RequestedEndAt, x.HoldExpiresUtcSeconds.HasValue ? DateTimeOffset.FromUnixTimeSeconds(x.HoldExpiresUtcSeconds.Value) : null),
            AppointmentType x => new AppointmentTypeDto(x.Id, x.Title, x.DurationMinutes, x.SortOrder, x.IsPublished),
            WorkingHoursRule x => new WorkingHoursRuleDto(x.Id, x.DayOfWeek, x.OpensAt.ToString("HH:mm"), x.ClosesAt.ToString("HH:mm"), x.SlotMinutes, x.IsClosed),
            WorkingHoursEventRule x => new WorkingHoursRuleDto(x.Id, x.DayOfWeek, x.OpensAt.ToString("HH:mm"), x.ClosesAt.ToString("HH:mm"), x.SlotMinutes, x.IsClosed),
            WorkingHoursEvent x => new WorkingHoursEventDto(x.Id, x.Title, x.Note, x.DisplayFrom, x.StartsOn, x.EndsOn, x.IsPublished, x.Rules.OrderBy(r => r.DayOfWeek).Select(Map<WorkingHoursRuleDto>).ToList()),
            IEnumerable<AppointmentRequest> x => x.Select(Map<AppointmentDto>).ToList(),
            IEnumerable<AppointmentType> x => x.Select(Map<AppointmentTypeDto>).ToList(),
            IEnumerable<ServiceItem> x => x.Select(Map<ServiceItemDto>).ToList(),
            IEnumerable<ArticleSection> x => x.Select(Map<ArticleSectionDto>).ToList(),
            IEnumerable<WorkingHoursRule> x => x.Select(Map<WorkingHoursRuleDto>).ToList(),
            IEnumerable<WorkingHoursEvent> x => x.Select(Map<WorkingHoursEventDto>).ToList(),
            _ => throw new InvalidOperationException("Unsupported DTO mapping.")
        };
        return (T)result;
    }
}
