using LmsTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();

        await db.Database.MigrateAsync();

        await SeedCoursesAsync(db);
        await EnsureMandatoryCoursesAsync(db);
        await EnsureEnterpriseStructureAsync(db);
        await SeedDemoAssignmentsAsync(db);
        await EnsureVarianceRecordsAsync(db);
        await EnsureMinimumAssignmentCoverageAsync(db);
    }

    private static async Task SeedCoursesAsync(LmsDbContext db)
    {
        if (await db.Courses.AnyAsync())
        {
            return;
        }

        db.Courses.AddRange(LocalCourseCatalog.BuildSeedCourses());
        await db.SaveChangesAsync();
    }

    private static async Task EnsureEnterpriseStructureAsync(LmsDbContext db)
    {
        if (!await db.Departments.AnyAsync())
        {
            var departments = new[]
            {
                new Department { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow },
                new Department { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Data & AI", Code = "DAI", CreatedAtUtc = DateTime.UtcNow },
                new Department { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Product & Design", Code = "PRD", CreatedAtUtc = DateTime.UtcNow }
            };

            db.Departments.AddRange(departments);
            await db.SaveChangesAsync();
        }

        if (!await db.Teams.AnyAsync())
        {
            var teams = new[]
            {
                new Team { Id = Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111"), DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Platform", ManagerName = "Ananya Rao", ManagerEmail = "ananya.rao@company.local", CreatedAtUtc = DateTime.UtcNow },
                new Team { Id = Guid.Parse("11111111-bbbb-bbbb-bbbb-111111111111"), DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "App Engineering", ManagerName = "Rohit Mehta", ManagerEmail = "rohit.mehta@company.local", CreatedAtUtc = DateTime.UtcNow },
                new Team { Id = Guid.Parse("22222222-aaaa-aaaa-aaaa-222222222222"), DepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "ML Engineering", ManagerName = "Priya Singh", ManagerEmail = "priya.singh@company.local", CreatedAtUtc = DateTime.UtcNow },
                new Team { Id = Guid.Parse("22222222-bbbb-bbbb-bbbb-222222222222"), DepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Analytics", ManagerName = "Vikas Nair", ManagerEmail = "vikas.nair@company.local", CreatedAtUtc = DateTime.UtcNow },
                new Team { Id = Guid.Parse("33333333-aaaa-aaaa-aaaa-333333333333"), DepartmentId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Product Strategy", ManagerName = "Sana Khan", ManagerEmail = "sana.khan@company.local", CreatedAtUtc = DateTime.UtcNow },
                new Team { Id = Guid.Parse("33333333-bbbb-bbbb-bbbb-333333333333"), DepartmentId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "UX Design", ManagerName = "Arjun Das", ManagerEmail = "arjun.das@company.local", CreatedAtUtc = DateTime.UtcNow }
            };

            db.Teams.AddRange(teams);
            await db.SaveChangesAsync();
        }

        var teamsByName = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Name, t => t.Id);
        if (!await db.Learners.AnyAsync())
        {
            var learners = new[]
            {
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1001", Name = "Aman Verma", Email = "aman.verma@company.local", Designation = "Senior Software Engineer", TeamId = teamsByName["Platform"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1002", Name = "Neha Iyer", Email = "neha.iyer@company.local", Designation = "Software Engineer", TeamId = teamsByName["Platform"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1003", Name = "Kabir Sharma", Email = "kabir.sharma@company.local", Designation = "DevOps Engineer", TeamId = teamsByName["Platform"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1101", Name = "Pooja Menon", Email = "pooja.menon@company.local", Designation = "Lead Engineer", TeamId = teamsByName["App Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1102", Name = "Harsh Patel", Email = "harsh.patel@company.local", Designation = "Software Engineer", TeamId = teamsByName["App Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1103", Name = "Riya Bhatt", Email = "riya.bhatt@company.local", Designation = "QA Engineer", TeamId = teamsByName["App Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2001", Name = "Nikhil Gupta", Email = "nikhil.gupta@company.local", Designation = "ML Engineer", TeamId = teamsByName["ML Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2002", Name = "Tanya Jain", Email = "tanya.jain@company.local", Designation = "Senior ML Engineer", TeamId = teamsByName["ML Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2003", Name = "Rahul Sethi", Email = "rahul.sethi@company.local", Designation = "Data Scientist", TeamId = teamsByName["ML Engineering"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2101", Name = "Meera Joshi", Email = "meera.joshi@company.local", Designation = "Analytics Lead", TeamId = teamsByName["Analytics"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2102", Name = "Karan Ahuja", Email = "karan.ahuja@company.local", Designation = "Business Analyst", TeamId = teamsByName["Analytics"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2103", Name = "Sonia Paul", Email = "sonia.paul@company.local", Designation = "Data Analyst", TeamId = teamsByName["Analytics"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3001", Name = "Aditya Kulkarni", Email = "aditya.kulkarni@company.local", Designation = "Product Manager", TeamId = teamsByName["Product Strategy"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3002", Name = "Shreya Nanda", Email = "shreya.nanda@company.local", Designation = "Associate Product Manager", TeamId = teamsByName["Product Strategy"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3003", Name = "Aditi Roy", Email = "aditi.roy@company.local", Designation = "Product Analyst", TeamId = teamsByName["Product Strategy"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3101", Name = "Omkar Deshmukh", Email = "omkar.deshmukh@company.local", Designation = "Senior UX Designer", TeamId = teamsByName["UX Design"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3102", Name = "Maya Reddy", Email = "maya.reddy@company.local", Designation = "UX Researcher", TeamId = teamsByName["UX Design"], CreatedAtUtc = DateTime.UtcNow },
                new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP3103", Name = "Ishaan Kapoor", Email = "ishaan.kapoor@company.local", Designation = "UI Designer", TeamId = teamsByName["UX Design"], CreatedAtUtc = DateTime.UtcNow }
            };

            db.Learners.AddRange(learners);
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureMandatoryCoursesAsync(LmsDbContext db)
    {
        var mandatoryCount = await db.Courses.CountAsync(c => c.IsMandatory);
        if (mandatoryCount > 0)
        {
            return;
        }

        var defaults = await db.Courses
            .OrderBy(c => c.Title)
            .Take(2)
            .ToListAsync();

        foreach (var course in defaults)
        {
            course.IsMandatory = true;
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoAssignmentsAsync(LmsDbContext db)
    {
        if (await db.Assignments.AnyAsync())
        {
            return;
        }

        var learners = await db.Learners.AsNoTracking().OrderBy(l => l.EmployeeCode).ToListAsync();
        var courses = await db.Courses.AsNoTracking().OrderBy(c => c.Title).Take(8).ToListAsync();
        if (learners.Count == 0 || courses.Count == 0)
        {
            return;
        }

        var assignments = new List<Assignment>();
        for (var i = 0; i < learners.Count; i++)
        {
            var primaryCourse = courses[i % courses.Count];
            var secondaryCourse = courses[(i + 3) % courses.Count];

            assignments.Add(BuildAssignment(learners[i], primaryCourse, i % 3));
            assignments.Add(BuildAssignment(learners[i], secondaryCourse, (i + 1) % 3));
        }

        db.Assignments.AddRange(assignments);
        await db.SaveChangesAsync();
    }

    private static Assignment BuildAssignment(Learner learner, Course course, int statusSeed)
    {
        var createdAt = DateTime.UtcNow.AddDays(-14 + statusSeed * 3);
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21 - statusSeed * 5));

        var progress = statusSeed switch
        {
            0 => 0,
            1 => 52,
            _ => 100
        };

        var status = AssignmentStatusExtensions.FromProgress(progress);

        return new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learner.Id,
            TeamId = learner.TeamId,
            CourseId = course.Id,
            AccessType = AccessType.Temporary,
            DueDate = dueDate,
            ProgressPercent = progress,
            Status = status,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt.AddDays(2)
        };
    }

    private static async Task EnsureVarianceRecordsAsync(LmsDbContext db)
    {
        var teamLookup = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Name, t => t.Id);
        if (teamLookup.Count == 0)
        {
            return;
        }

        var learnerSeed = new[]
        {
            new { EmployeeCode = "EMP2201", Name = "Dev Malhotra", Email = "dev.malhotra@company.local", Designation = "Junior ML Engineer", TeamName = "ML Engineering" },
            new { EmployeeCode = "EMP2202", Name = "Anika Suri", Email = "anika.suri@company.local", Designation = "ML Engineer", TeamName = "ML Engineering" },
            new { EmployeeCode = "EMP2203", Name = "Ritvik Bansal", Email = "ritvik.bansal@company.local", Designation = "MLOps Engineer", TeamName = "ML Engineering" },
            new { EmployeeCode = "EMP2204", Name = "Naina Puri", Email = "naina.puri@company.local", Designation = "Data Engineer", TeamName = "ML Engineering" },
            new { EmployeeCode = "EMP2104", Name = "Aarav Chawla", Email = "aarav.chawla@company.local", Designation = "Reporting Analyst", TeamName = "Analytics" },
            new { EmployeeCode = "EMP2105", Name = "Tanvi Arora", Email = "tanvi.arora@company.local", Designation = "BI Analyst", TeamName = "Analytics" },
            new { EmployeeCode = "EMP1201", Name = "Kushagra Jain", Email = "kushagra.jain@company.local", Designation = "Platform Engineer", TeamName = "Platform" },
            new { EmployeeCode = "EMP3004", Name = "Ira Mathur", Email = "ira.mathur@company.local", Designation = "Product Operations", TeamName = "Product Strategy" }
        };

        var anyLearnerAdded = false;
        foreach (var seed in learnerSeed)
        {
            if (!teamLookup.TryGetValue(seed.TeamName, out var teamId))
            {
                continue;
            }

            var exists = await db.Learners.AnyAsync(l => l.EmployeeCode == seed.EmployeeCode);
            if (exists)
            {
                continue;
            }

            db.Learners.Add(new Learner
            {
                Id = Guid.NewGuid(),
                EmployeeCode = seed.EmployeeCode,
                Name = seed.Name,
                Email = seed.Email,
                Designation = seed.Designation,
                TeamId = teamId,
                CreatedAtUtc = DateTime.UtcNow
            });

            anyLearnerAdded = true;
        }

        if (anyLearnerAdded)
        {
            await db.SaveChangesAsync();
        }

        var learnerMap = await db.Learners
            .AsNoTracking()
            .ToDictionaryAsync(l => l.EmployeeCode, l => new { l.Id, l.TeamId });

        var courseIds = await db.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => c.Id)
            .ToListAsync();

        if (courseIds.Count == 0)
        {
            return;
        }

        var extraAssignmentsPlan = new[]
        {
            new { EmployeeCode = "EMP2001", CourseOffset = 12, Seed = 2 },
            new { EmployeeCode = "EMP2002", CourseOffset = 13, Seed = 1 },
            new { EmployeeCode = "EMP2003", CourseOffset = 14, Seed = 0 },
            new { EmployeeCode = "EMP2201", CourseOffset = 15, Seed = 1 },
            new { EmployeeCode = "EMP2202", CourseOffset = 16, Seed = 2 },
            new { EmployeeCode = "EMP2203", CourseOffset = 17, Seed = 0 },
            new { EmployeeCode = "EMP2204", CourseOffset = 18, Seed = 1 },
            new { EmployeeCode = "EMP2101", CourseOffset = 19, Seed = 2 },
            new { EmployeeCode = "EMP2102", CourseOffset = 20, Seed = 1 },
            new { EmployeeCode = "EMP2104", CourseOffset = 21, Seed = 0 },
            new { EmployeeCode = "EMP2105", CourseOffset = 22, Seed = 1 },
            new { EmployeeCode = "EMP1001", CourseOffset = 23, Seed = 2 },
            new { EmployeeCode = "EMP1201", CourseOffset = 24, Seed = 1 },
            new { EmployeeCode = "EMP3001", CourseOffset = 25, Seed = 0 },
            new { EmployeeCode = "EMP3004", CourseOffset = 26, Seed = 1 },
            new { EmployeeCode = "EMP3101", CourseOffset = 27, Seed = 2 }
        };

        var existingPairs = await db.Assignments
            .AsNoTracking()
            .Select(a => new { a.LearnerId, a.CourseId })
            .ToListAsync();

        var pairSet = existingPairs
            .Select(x => $"{x.LearnerId:N}:{x.CourseId:N}")
            .ToHashSet();

        foreach (var item in extraAssignmentsPlan)
        {
            if (!learnerMap.TryGetValue(item.EmployeeCode, out var learner))
            {
                continue;
            }

            var courseId = courseIds[item.CourseOffset % courseIds.Count];
            var key = $"{learner.Id:N}:{courseId:N}";
            if (pairSet.Contains(key))
            {
                continue;
            }

            db.Assignments.Add(BuildSkewedAssignment(learner.Id, learner.TeamId, courseId, item.Seed));
            pairSet.Add(key);
        }

        await db.SaveChangesAsync();
    }

    private static Assignment BuildSkewedAssignment(Guid learnerId, Guid? teamId, Guid courseId, int seed)
    {
        var progress = seed switch
        {
            0 => 0,
            1 => 40,
            _ => 100
        };

        var status = AssignmentStatusExtensions.FromProgress(progress);

        var createdAt = DateTime.UtcNow.AddDays(-9 + seed * 2);

        return new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learnerId,
            TeamId = teamId,
            CourseId = courseId,
            AccessType = AccessType.Temporary,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(18 - seed * 3)),
            ProgressPercent = progress,
            Status = status,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt.AddDays(1)
        };
    }

    private static async Task EnsureMinimumAssignmentCoverageAsync(LmsDbContext db)
    {
        var learners = await db.Learners.AsNoTracking().ToListAsync();
        if (learners.Count == 0)
        {
            return;
        }

        var existingLearnerIds = await db.Assignments
            .AsNoTracking()
            .Select(a => a.LearnerId)
            .Distinct()
            .ToListAsync();

        var assignedSet = existingLearnerIds.ToHashSet();
        var missingLearners = learners.Where(l => !assignedSet.Contains(l.Id)).ToList();
        if (missingLearners.Count == 0)
        {
            return;
        }

        var firstMandatoryCourse = await db.Courses
            .AsNoTracking()
            .Where(c => c.IsMandatory)
            .OrderBy(c => c.Title)
            .FirstOrDefaultAsync();

        var fallbackCourse = firstMandatoryCourse
            ?? await db.Courses.AsNoTracking().OrderBy(c => c.Title).FirstOrDefaultAsync();

        if (fallbackCourse is null)
        {
            return;
        }

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        db.Assignments.AddRange(missingLearners.Select(learner => new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learner.Id,
            TeamId = learner.TeamId,
            CourseId = fallbackCourse.Id,
            AccessType = AccessType.Temporary,
            DueDate = dueDate,
            ProgressPercent = 0,
            Status = AssignmentStatus.NotStarted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }));

        await db.SaveChangesAsync();
    }
}
