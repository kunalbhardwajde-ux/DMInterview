namespace LmsTracker.Api.Domain;

public sealed class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Team
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Learner
{
    public Guid Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public Guid? TeamId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Course
{
    public Guid Id { get; set; }
    public string ExternalCourseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string LaunchUrl { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }

    // Optional LLM-extracted skill/technology keywords (e.g. "kubernetes", "data-visualization"),
    // populated at course-sync time when AnthropicSkillTagging is enabled - see
    // Infrastructure/Ai/ISkillTagExtractor.cs. Empty when extraction is disabled or hasn't run
    // yet; skill matching always falls back to title-substring matching in that case.
    public List<string> SkillTags { get; set; } = [];

    // Durable work marker: set by CourseCatalogService.SyncFromProvidersAsync when a touched,
    // still-untagged course needs extraction; cleared by ProcessPendingSkillTagExtractionsAsync
    // once attempted (tagged or not - extraction fails open, so "attempted" is terminal until the
    // next sync re-flags it). Living in this column rather than an in-memory queue is what makes
    // extraction survive a process crash between "sync completed" and "extraction ran" - see
    // Infrastructure/BackgroundTasks/SkillTagExtractionPollingService.cs.
    public DateTime? SkillTagExtractionRequestedAtUtc { get; set; }
}

public sealed class Assignment
{
    public Guid Id { get; set; }
    public Guid LearnerId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid CourseId { get; set; }
    public AccessType AccessType { get; set; }
    public DateOnly? DueDate { get; set; }
    public int ProgressPercent { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum AccessType
{
    Temporary,
    Permanent,
}

public enum AssignmentStatus
{
    NotStarted,
    InProgress,
    Completed,
}

public static class AssignmentStatusExtensions
{
    public static AssignmentStatus FromProgress(int progressPercent) => progressPercent switch
    {
        0 => AssignmentStatus.NotStarted,
        100 => AssignmentStatus.Completed,
        _ => AssignmentStatus.InProgress,
    };
}

// Auto-populated by LmsDbContext.SaveChangesAsync from the EF change tracker - every Added/
// Modified/Deleted entity in a save becomes one row here, so no service method has to remember
// to call an audit logger. Actor is the best identity SaveChangesAsync can attribute: a real
// employee code for a Learner token, or "Manager (session <jti prefix>)" for the shared Manager
// credential - see the README's Authentication section for why that's still not a named person.
public sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
}
