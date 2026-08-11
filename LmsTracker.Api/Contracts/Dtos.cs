using System.ComponentModel.DataAnnotations;

namespace LmsTracker.Api.Contracts;

public sealed record CreateDepartmentRequest(
    [property: Required, MinLength(2)] string Name,
    [property: Required, MinLength(2)] string Code);

// Cross-field/trim-aware rules (e.g. "2 chars after trimming", "valid department + valid email")
// aren't expressible as simple per-property attributes, so this implements IValidatableObject
// instead of hand-rolling a separate validator class - Validator.TryValidateObject (called from
// RequestValidationHelper, the single validation entry point every request DTO goes through)
// invokes this automatically alongside any attribute checks.
public sealed record CreateTeamRequest(
    string Name,
    Guid DepartmentId,
    string ManagerName,
    string ManagerEmail) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedName = Name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 2)
        {
            yield return new ValidationResult("Team name must be at least 2 characters long.", [nameof(Name)]);
        }

        if (DepartmentId == Guid.Empty)
        {
            yield return new ValidationResult("A valid department must be selected.", [nameof(DepartmentId)]);
        }

        var trimmedManagerName = ManagerName?.Trim() ?? string.Empty;
        if (trimmedManagerName.Length < 2)
        {
            yield return new ValidationResult("Manager name must be at least 2 characters long.", [nameof(ManagerName)]);
        }

        var trimmedManagerEmail = ManagerEmail?.Trim() ?? string.Empty;
        if (trimmedManagerEmail.Length < 5 || !System.Net.Mail.MailAddress.TryCreate(trimmedManagerEmail, out _))
        {
            yield return new ValidationResult("A valid manager email address is required.", [nameof(ManagerEmail)]);
        }
    }
}

public sealed record CreateLearnerRequest(
    [property: Required, MinLength(2)] string EmployeeCode,
    [property: Required, MinLength(2)] string Name,
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(2)] string Designation,
    Guid? TeamId);

// See CreateTeamRequest above for why this is IValidatableObject rather than a separate rules
// class: "course/team must be selected", "at least one of learner or team", and "due date
// required for temporary access" are cross-field rules attributes alone can't express.
public sealed record CreateAssignmentRequest(
    Guid CourseId,
    Guid? LearnerId,
    Guid? TeamId,
    [property: Required] string AccessType,
    DateOnly? DueDate) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CourseId == Guid.Empty)
        {
            yield return new ValidationResult("A valid course must be selected.", [nameof(CourseId)]);
        }

        if (!LearnerId.HasValue && !TeamId.HasValue)
        {
            yield return new ValidationResult("Please select a learner or a team for the assignment.", [nameof(LearnerId), nameof(TeamId)]);
        }

        if (!Enum.TryParse<LmsTracker.Api.Domain.AccessType>(AccessType, true, out var parsedAccessType))
        {
            yield return new ValidationResult("Access type must be Temporary or Permanent.", [nameof(AccessType)]);
        }
        else if (parsedAccessType == LmsTracker.Api.Domain.AccessType.Temporary && DueDate is null)
        {
            yield return new ValidationResult("A due date is required for temporary access.", [nameof(DueDate)]);
        }
    }
}

public sealed record UpdateProgressRequest([property: Range(0, 100)] int ProgressPercent);
public sealed record UpdateMandatoryCourseRequest(bool IsMandatory);

// Exactly one of AccessCode (Manager) or EmployeeCode (Learner) must be supplied - see
// CreateTeamRequest above for why cross-field "exactly one of" rules live in Validate()
// rather than a per-property attribute.
public sealed record LoginRequest(string? AccessCode, string? EmployeeCode) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAccessCode = !string.IsNullOrWhiteSpace(AccessCode);
        var hasEmployeeCode = !string.IsNullOrWhiteSpace(EmployeeCode);

        if (hasAccessCode == hasEmployeeCode)
        {
            yield return new ValidationResult(
                "Provide exactly one of accessCode (manager sign-in) or employeeCode (learner sign-in).",
                [nameof(AccessCode), nameof(EmployeeCode)]);
        }
    }
}

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, string Role);

public record DepartmentView(Guid Id, string Name, string Code);
public record TeamView(Guid Id, string Name, Guid DepartmentId, string DepartmentName, string ManagerName, string ManagerEmail);
public record LearnerView(
    Guid Id,
    string EmployeeCode,
    string Name,
    string Email,
    string Designation,
    Guid? TeamId,
    string? TeamName,
    Guid? DepartmentId,
    string? DepartmentName);

public record AssignmentView(
    Guid Id,
    Guid LearnerId,
    string EmployeeCode,
    string LearnerName,
    Guid? DepartmentId,
    string? DepartmentName,
    string? TeamName,
    Guid CourseId,
    string CourseTitle,
    string Provider,
    string LaunchUrl,
    string AccessType,
    string? DueDate,
    int ProgressPercent,
    string Status);

public record DashboardScopeResponse(
    int Departments,
    int Teams,
    int Learners,
    int Assignments,
    int Completed,
    int InProgress,
    int NotStarted,
    int Overdue,
    double CompletionRate);

public record MandatoryComplianceRow(
    Guid LearnerId,
    string EmployeeCode,
    string LearnerName,
    string? DepartmentName,
    string? TeamName,
    int MandatoryCourses,
    int CompletedMandatoryCourses,
    int PendingMandatoryCourses,
    string PendingCourseTitles);

public record SkillMatchRow(
    Guid LearnerId,
    string EmployeeCode,
    string LearnerName,
    string? DepartmentName,
    string? TeamName,
    int CompletedCourses,
    int MatchedCourses,
    int MatchedSkills,
    int MatchScore,
    string PriorityTier,
    string MatchedSkillKeywords,
    string MissingSkillKeywords,
    string TopMatchedCourses,
    string ScoreBreakdown);

public record AuditLogEntryView(Guid Id, DateTime TimestampUtc, string Actor, string Action, string EntityType, string? EntityId);
