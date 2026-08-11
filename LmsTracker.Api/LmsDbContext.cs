using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using LmsTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LmsTracker.Api;

// httpContextAccessor is optional (default null) so tests that construct this context directly
// via `new LmsDbContext(options)` (bypassing DI) keep compiling - SaveChangesAsync just attributes
// those saves to "System" instead of a real actor, which is correct: there's no HTTP caller there.
public sealed class LmsDbContext(DbContextOptions<LmsDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    // Auto-audit hook: inspects the change tracker for every Added/Modified/Deleted entity in
    // this save and writes one AuditLogEntry per entity, in the same SaveChanges call - see
    // AuditLogEntry's doc comment for why this lives here instead of being threaded through every
    // service method individually.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = BuildAuditEntries();
        if (auditEntries.Count > 0)
        {
            AuditLogEntries.AddRange(auditEntries);
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private List<AuditLogEntry> BuildAuditEntries()
    {
        var actor = DescribeActor(httpContextAccessor?.HttpContext?.User);
        var timestampUtc = DateTime.UtcNow;

        var entries = new List<AuditLogEntry>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLogEntry || entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var keyValues = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue?.ToString())
                .Where(v => v is not null);

            entries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = timestampUtc,
                Actor = actor,
                Action = entry.State.ToString(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = string.Join(",", keyValues),
            });
        }

        return entries;
    }

    private static string DescribeActor(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "System";
        }

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "Unknown";
        if (role == "Learner")
        {
            var employeeCode = user.FindFirstValue("employee_code");
            return $"Learner:{employeeCode ?? "unknown"}";
        }

        // Manager is a single shared credential - there's no per-manager identity to attribute
        // to. The token's jti at least distinguishes "this login session" from another, which is
        // the most specific attribution possible without adding real per-manager accounts.
        var jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var sessionTag = jti is { Length: >= 8 } ? jti[..8] : jti ?? "unknown";
        return $"Manager (session {sessionTag})";
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DepartmentId).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ManagerName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ManagerEmail).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.DepartmentId, x.Name }).IsUnique();

            entity.HasOne<Department>()
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Learner>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Designation).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.EmployeeCode).IsUnique();

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalCourseId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LaunchUrl).HasMaxLength(300).IsRequired();
            entity.Property(x => x.IsMandatory).HasDefaultValue(false);
            entity.HasIndex(x => new { x.Provider, x.ExternalCourseId }).IsUnique();
            // The poller's whole query is "WHERE SkillTagExtractionRequestedAtUtc IS NOT NULL" -
            // a filtered index keeps that cheap regardless of catalog size, since the vast
            // majority of rows have this column null.
            entity.HasIndex(x => x.SkillTagExtractionRequestedAtUtc)
                .HasFilter("[SkillTagExtractionRequestedAtUtc] IS NOT NULL");

            // Stored as a JSON array string. EF's default change tracking uses reference
            // equality for converted reference types, so a plain conversion here would silently
            // miss in-place list mutations - the ValueComparer makes SaveChangesAsync detect
            // content changes too, not just reference swaps.
            entity.Property(x => x.SkillTags)
                .HasConversion(
                    tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
                    json => string.IsNullOrWhiteSpace(json)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                    v => v.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                    v => v.ToList()));
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccessType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.RowVersion).IsRowVersion();

            // Supports two hot paths: the assignment-creation idempotency check (course + active
            // status) and the skill-match/dashboard "completed assignments for these learners"
            // scans, which filter on Status after the FK-indexed LearnerId narrows the row set.
            entity.HasIndex(x => new { x.CourseId, x.Status });

            entity.HasOne<Learner>()
                .WithMany()
                .HasForeignKey(x => x.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Course>()
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(200);
            // The admin view (GET /api/audit-log) always orders newest-first, and this is a
            // write-heavy, read-occasionally table - a descending index on the sort column is the
            // one index worth paying insert cost for here.
            entity.HasIndex(x => x.TimestampUtc).IsDescending();
        });
    }
}
