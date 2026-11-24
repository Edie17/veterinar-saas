using System.Globalization;
using System.Text;
using Veterinar.Domain.Common;

namespace Veterinar.Domain.Clinics;

public sealed class Clinic : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string TimeZone { get; set; } = "Europe/Prague";

    public static Clinic Create(string name, string? slug = null, string timeZone = "Europe/Prague")
    {
        var cleanName = NormalizeWhitespace(name);
        return new Clinic
        {
            Name = cleanName,
            Slug = Slugify(string.IsNullOrWhiteSpace(slug) ? cleanName : slug),
            TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "Europe/Prague" : timeZone.Trim(),
        };
    }

    public static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            var ascii = TransliterateCzech(ch);
            if (char.IsLetterOrDigit(ascii))
            {
                builder.Append(ascii);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static char TransliterateCzech(char ch) => ch switch
    {
        'á' or 'à' or 'ä' or 'â' => 'a',
        'č' => 'c',
        'ď' => 'd',
        'é' or 'ě' or 'ë' or 'ê' => 'e',
        'í' or 'ï' or 'î' => 'i',
        'ň' => 'n',
        'ó' or 'ö' or 'ô' => 'o',
        'ř' => 'r',
        'š' => 's',
        'ť' => 't',
        'ú' or 'ů' or 'ü' or 'û' => 'u',
        'ý' or 'ÿ' => 'y',
        'ž' => 'z',
        _ => ch
    };

    private static string NormalizeWhitespace(string value) => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
