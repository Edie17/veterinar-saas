using Veterinar.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Auth;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Missing_User_Still_Verifies_Once_And_Cannot_Authenticate()
    {
        await using var db = new FakeAppDbContext();
        var hasher = new RecordingPasswordHasher();
        var result = await new AuthService(new FakeClinicUserRepository(db), hasher, CreateMapper())
            .ValidateAdminAsync(new LoginRequest("unknown@example.cz", "wrong-password"));
        Assert.Null(result);
        Assert.Equal(1, hasher.Calls);
    }
    private sealed class RecordingPasswordHasher : IPasswordHasher
    {
        public int Calls { get; private set; }
        public string Hash(string password) => "unused";
        public bool Verify(string password, string? encodedHash) { Calls++; return true; }
    }
    [Fact]
    public async Task ValidateClinicUserAsync_Returns_Clinic_Context_Claims_For_Active_User()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        var hasher = new FakePasswordHasher();
        db.Clinics.Add(new Clinic { Id = clinicId, Name = "Vet", Slug = "vet", TimeZone = "Europe/Prague" });
        db.ClinicUsers.Add(new ClinicUser
        {
            ClinicId = clinicId,
            Email = "owner@example.cz",
            PasswordHash = hasher.Hash("heslo"),
            Role = ClinicUserRole.Owner
        });
        await db.SaveChangesAsync();

        var result = await new AuthService(new FakeClinicUserRepository(db), hasher, CreateMapper()).ValidateAdminAsync(new LoginRequest("OWNER@example.cz", "heslo"));

        Assert.NotNull(result);
        Assert.Equal(clinicId, result!.ClinicId);
        Assert.Equal("Owner", result.Role);
    }

    private static DtoMapper CreateMapper() => new DtoMapper();

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string? passwordHash) => passwordHash == Hash(password);
    }

    private sealed class FakeClinicUserRepository(FakeAppDbContext db) : IClinicUserRepository
    {
        public Task<ClinicUser?> FindActiveByIdAsync(Guid id, CancellationToken ct = default) => db.ClinicUsers.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        public Task<ClinicUser?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            db.ClinicUsers.SingleOrDefaultAsync(user => user.Email == normalizedEmail && user.IsActive, cancellationToken);
    }

    private sealed class FakeAppDbContext : DbContext
    {
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<Clinic> Clinics => Set<Clinic>();
        public DbSet<ClinicUser> ClinicUsers => Set<ClinicUser>();
        public DbSet<PageContent> PageContents => Set<PageContent>();
        public DbSet<ServiceItem> Services => Set<ServiceItem>();
        public DbSet<ArticleSection> ArticleSections => Set<ArticleSection>();
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
        public DbSet<AppointmentRequest> AppointmentRequests => Set<AppointmentRequest>();
        public DbSet<WorkingHoursRule> WorkingHours => Set<WorkingHoursRule>();
        public DbSet<WorkingHoursEvent> WorkingHoursEvents => Set<WorkingHoursEvent>();
        public DbSet<WorkingHoursEventRule> WorkingHoursEventRules => Set<WorkingHoursEventRule>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }
    }
}
