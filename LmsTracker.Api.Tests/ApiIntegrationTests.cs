using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LmsTracker.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTracker.Api.Tests;

/// <summary>
/// End-to-end HTTP tests through the real ASP.NET Core pipeline (routing, model binding, auth,
/// ApiResultExtensions status-code mapping) - everything the unit/service-level tests can't see.
/// Every non-auth/non-health endpoint now requires a bearer token, so this fixture logs in once
/// via the real /api/auth/login endpoint (not a test-only bypass) and reuses the token for every
/// test - the login flow itself is exercised by AuthEndpoint_Tests below.
/// </summary>
public sealed class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.TestApiFactory>, IAsyncLifetime
{
    private readonly TestApiFactory _factory;
    private HttpClient _client = null!;

    public ApiIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        var token = await LoginAsync(_client, TestApiFactory.ManagerAccessCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<string> LoginAsync(HttpClient client, string accessCode)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { accessCode });
        response.EnsureSuccessStatusCode();
        using var json = await ParseAsync(response);
        return json.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private static async Task<string> LoginAsLearnerAsync(HttpClient client, string employeeCode)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { employeeCode });
        response.EnsureSuccessStatusCode();
        using var json = await ParseAsync(response);
        return json.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task<(Guid Id, string EmployeeCode)> CreateLearnerAsync(string suffix)
    {
        var response = await _client.PostAsJsonAsync("/api/learners", new
        {
            employeeCode = $"LRN{suffix}",
            name = $"Learner {suffix}",
            email = $"learner{suffix}@example.com",
            designation = "Engineer",
            teamId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();
        using var json = await ParseAsync(response);
        var id = Guid.Parse(json.RootElement.GetProperty("data").GetProperty("id").GetString()!);
        // The server normalizes (trims + uppercases) the employee code before storing it - read
        // back what it actually stored rather than assuming the request body's casing survived.
        var employeeCode = json.RootElement.GetProperty("data").GetProperty("employeeCode").GetString()!;
        return (id, employeeCode);
    }

    [Fact]
    public async Task Health_ReturnsOkEnvelope_WithoutAuthentication()
    {
        // Health checks are infra/load-balancer probes - deliberately not behind auth. Uses a
        // fresh, unauthenticated client to prove this, rather than relying on _client's token.
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ok", json.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Login_WithCorrectAccessCode_ReturnsUsableToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { accessCode = TestApiFactory.ManagerAccessCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ParseAsync(response);
        var token = json.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var protectedResponse = await client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongAccessCode_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { accessCode = "definitely-not-the-code" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.Equal("UNAUTHORIZED", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/departments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDepartment_WithInvalidBody_Returns400WithValidationErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/departments", new { name = "", code = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_FAILED", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task CreateDepartment_ThenList_RoundTripsThroughTheRealHttpPipeline()
    {
        var code = $"INT{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var createResponse = await _client.PostAsJsonAsync("/api/departments", new { name = "Integration Dept", code });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var json = await ParseAsync(listResponse);
        var codes = json.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("code").GetString());
        Assert.Contains(code, codes);
    }

    [Fact]
    public async Task CreateTeam_WithMissingDepartment_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/teams", new
        {
            name = "Ghost Team",
            departmentId = Guid.NewGuid(),
            managerName = "Nobody",
            managerEmail = "nobody@example.com",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ParseAsync(response);
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString());
        Assert.Contains(errors, e => e!.Contains("Department not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateProgress_ForMissingAssignment_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/assignments/{Guid.NewGuid()}/progress")
        {
            Content = JsonContent.Create(new { progressPercent = 50 }),
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.Equal("NOT_FOUND", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListLearners_WithoutPagingParams_ReturnsFullListAndNoTotalCountHeader()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        foreach (var index in Enumerable.Range(1, 3))
        {
            await _client.PostAsJsonAsync("/api/learners", new
            {
                employeeCode = $"NOPAGE{suffix}{index}",
                name = $"No Paging Learner {index}",
                email = $"nopage{suffix}{index}@example.com",
                designation = "Engineer",
                teamId = (Guid?)null,
            });
        }

        var response = await _client.GetAsync("/api/learners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));
        using var json = await ParseAsync(response);
        var names = json.RootElement.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("name").GetString());
        Assert.Contains($"No Paging Learner 1", names);
        Assert.Contains($"No Paging Learner 3", names);
    }

    [Fact]
    public async Task ListLearners_WithPagingParams_ReturnsOnlyOnePageAndSetsTotalCountHeader()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        foreach (var index in Enumerable.Range(1, 3))
        {
            await _client.PostAsJsonAsync("/api/learners", new
            {
                employeeCode = $"PAGED{suffix}{index}",
                name = $"Paged Learner {suffix}{index}",
                email = $"paged{suffix}{index}@example.com",
                designation = "Engineer",
                teamId = (Guid?)null,
            });
        }

        var response = await _client.GetAsync("/api/learners?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        var totalCount = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
        Assert.True(totalCount >= 3);

        using var json = await ParseAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ListTeams_WithPagingParams_ReturnsOnlyOnePageAndSetsTotalCountHeader()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var deptResponse = await _client.PostAsJsonAsync("/api/departments", new { name = $"Paging Dept {suffix}", code = $"PD{suffix}" });
        using var deptJson = await ParseAsync(deptResponse);
        var departmentId = deptJson.RootElement.GetProperty("data").GetProperty("id").GetString();

        foreach (var index in Enumerable.Range(1, 3))
        {
            await _client.PostAsJsonAsync("/api/teams", new
            {
                name = $"Paged Team {suffix}{index}",
                departmentId,
                managerName = "Manager",
                managerEmail = $"manager{suffix}{index}@example.com",
            });
        }

        var response = await _client.GetAsync("/api/teams?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        var totalCount = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
        Assert.True(totalCount >= 3);

        using var json = await ParseAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ListCourses_WithPagingParams_ReturnsOnlyOnePageAndSetsTotalCountHeader()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            db.Courses.AddRange(Enumerable.Range(1, 3).Select(index => new Course
            {
                Id = Guid.NewGuid(),
                ExternalCourseId = $"paging-{suffix}-{index}",
                Title = $"Paging Course {suffix} {index}",
                Provider = "Udemy",
                LaunchUrl = "https://example.com",
            }));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/courses?query=Paging%20Course%20{suffix}&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        var totalCount = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal(3, totalCount);

        using var json = await ParseAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Login_WithEmployeeCode_ReturnsLearnerTokenAndRole()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (_, employeeCode) = await CreateLearnerAsync(suffix);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { employeeCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.Equal("Learner", json.RootElement.GetProperty("data").GetProperty("role").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("data").GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_WithBothAccessCodeAndEmployeeCode_Returns400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            accessCode = TestApiFactory.ManagerAccessCode,
            employeeCode = "SOMEONE",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithNeitherCredential_Returns400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmployeeCode_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { employeeCode = "DOES-NOT-EXIST-XYZ" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // One representative route isn't enough to back "every Manager-only endpoint rejects a
    // Learner token" - the policy is applied uniformly per-module (see each Module's
    // RequireAuthorization("ManagerOnly") group), but that uniformity is exactly the kind of
    // thing a future edit could quietly break in one module without the others. This asserts it
    // per-module instead of trusting the pattern held everywhere.
    [Theory]
    [InlineData("/api/departments")]
    [InlineData("/api/teams")]
    [InlineData("/api/learners")]
    [InlineData("/api/courses")]
    [InlineData("/api/assignments")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/reports/mandatory-compliance")]
    [InlineData("/api/audit-log")]
    [InlineData("/api/integrations/udemy/status")]
    [InlineData("/api/integrations/linkedin/status")]
    public async Task LearnerToken_CannotAccessAnyManagerOnlyEndpoint_Returns403(string managerOnlyPath)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (_, employeeCode) = await CreateLearnerAsync(suffix);
        using var learnerClient = _factory.CreateClient();
        var learnerToken = await LoginAsLearnerAsync(learnerClient, employeeCode);
        learnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", learnerToken);

        var response = await learnerClient.GetAsync(managerOnlyPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagerToken_CannotAccessLearnerSelfServiceEndpoint_Returns403()
    {
        var response = await _client.GetAsync("/api/learners/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LearnerMe_ReturnsOwnProfile()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (learnerId, employeeCode) = await CreateLearnerAsync(suffix);
        using var learnerClient = _factory.CreateClient();
        var learnerToken = await LoginAsLearnerAsync(learnerClient, employeeCode);
        learnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", learnerToken);

        var response = await learnerClient.GetAsync("/api/learners/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ParseAsync(response);
        Assert.Equal(learnerId.ToString(), json.RootElement.GetProperty("data").GetProperty("id").GetString());
        Assert.Equal(employeeCode, json.RootElement.GetProperty("data").GetProperty("employeeCode").GetString());
    }

    [Fact]
    public async Task LearnerMine_ReturnsOnlyThatLearnersOwnAssignments_NeverAnotherLearners()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (learnerAId, employeeCodeA) = await CreateLearnerAsync($"A{suffix}");
        var (learnerBId, _) = await CreateLearnerAsync($"B{suffix}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            var course = new Course
            {
                Id = Guid.NewGuid(),
                ExternalCourseId = $"mine-{suffix}",
                Title = $"Mine Test Course {suffix}",
                Provider = "Udemy",
                LaunchUrl = "https://example.com",
            };
            db.Courses.Add(course);
            db.Assignments.AddRange(
                new Assignment { Id = Guid.NewGuid(), LearnerId = learnerAId, CourseId = course.Id, AccessType = AccessType.Permanent, Status = AssignmentStatus.NotStarted },
                new Assignment { Id = Guid.NewGuid(), LearnerId = learnerBId, CourseId = course.Id, AccessType = AccessType.Permanent, Status = AssignmentStatus.NotStarted });
            await db.SaveChangesAsync();
        }

        using var learnerClient = _factory.CreateClient();
        var learnerToken = await LoginAsLearnerAsync(learnerClient, employeeCodeA);
        learnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", learnerToken);

        var response = await learnerClient.GetAsync("/api/assignments/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ParseAsync(response);
        var learnerIds = json.RootElement.GetProperty("data").EnumerateArray()
            .Select(x => x.GetProperty("learnerId").GetString())
            .ToList();
        Assert.All(learnerIds, id => Assert.Equal(learnerAId.ToString(), id));
        Assert.DoesNotContain(learnerBId.ToString(), learnerIds);
    }

    [Fact]
    public async Task AuditLog_RecordsAMutation_AttributedToTheManagerSession()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var code = $"AUD{suffix}".ToUpperInvariant();

        var createResponse = await _client.PostAsJsonAsync("/api/departments", new { name = $"Audit Dept {suffix}", code });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createdJson = await ParseAsync(createResponse);
        var departmentId = createdJson.RootElement.GetProperty("data").GetProperty("id").GetString();

        var auditResponse = await _client.GetAsync("/api/audit-log?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        using var auditJson = await ParseAsync(auditResponse);
        var entries = auditJson.RootElement.GetProperty("data").EnumerateArray().ToList();
        var match = entries.SingleOrDefault(e =>
            e.GetProperty("entityType").GetString() == "Department"
            && e.GetProperty("entityId").GetString() == departmentId);

        Assert.True(match.ValueKind != JsonValueKind.Undefined, "Expected an audit-log entry for the newly created department.");
        Assert.Equal("Added", match.GetProperty("action").GetString());
        Assert.StartsWith("Manager", match.GetProperty("actor").GetString());
    }

    [Fact]
    public async Task AuditLog_LearnerToken_CannotAccess_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (_, employeeCode) = await CreateLearnerAsync(suffix);
        using var learnerClient = _factory.CreateClient();
        var learnerToken = await LoginAsLearnerAsync(learnerClient, employeeCode);
        learnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", learnerToken);

        var response = await learnerClient.GetAsync("/api/audit-log");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimiting_Rejects429AfterConfiguredLimit_WithinTheWindow()
    {
        // A derived factory (fresh host, fresh in-memory rate-limiter state) with a tiny limit
        // layered on top of the shared fixture's config, so this test can actually observe the
        // 429 path without needing 100k requests or throttling every other test's logins.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:LoginPermitLimit"] = "3",
                });
            });
        });
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { accessCode = "wrong-code" });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("/api/auth/login", new { accessCode = "wrong-code" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        using var json = await ParseAsync(limitedResponse);
        Assert.Equal("RATE_LIMITED", json.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        public const string ManagerAccessCode = "integration-test-access-code";
        private const string SigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Deterministic, test-only auth config - never the real appsettings values, so a
            // token minted here can never be mistaken for (or replayed against) a real deployment.
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:SigningKey"] = SigningKey,
                    ["Auth:Issuer"] = "LmsTracker.Tests",
                    ["Auth:Audience"] = "LmsTracker.Tests.Clients",
                    ["Auth:ManagerAccessCode"] = ManagerAccessCode,
                    ["Auth:TokenLifetimeMinutes"] = "60",
                    // The shared IClassFixture instance logs in at least once per test method - a
                    // production-sized 5/minute limit would throttle the test run itself, not just
                    // an attacker. Effectively unlimited here; Login_RateLimiting_Rejects429...
                    // below layers a tiny override onto its own isolated factory instance via
                    // WithWebHostBuilder instead of touching this shared one.
                    ["RateLimiting:LoginPermitLimit"] = "100000",
                });
            });

            builder.ConfigureServices(services =>
            {
                // AddDbContext<LmsDbContext>(UseSqlServer) registers more than just
                // DbContextOptions<LmsDbContext> (EF Core also wires up an
                // IDbContextOptionsConfiguration<LmsDbContext> entry) - removing only the
                // options descriptor leaves the SqlServer extension registered alongside the
                // InMemory one added below, which EF rejects. Strip everything closed over
                // LmsDbContext instead of guessing the exact internal type names.
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType.IsGenericType
                        && (d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") ?? false)
                        && d.ServiceType.GenericTypeArguments.Contains(typeof(LmsDbContext)))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Compute the name once, outside the configure delegate - the delegate itself
                // runs on every DbContext construction (i.e. every request scope), so a
                // Guid.NewGuid() call inside it would hand each request a fresh empty database.
                var databaseName = $"integration-{Guid.NewGuid()}";
                services.AddDbContext<LmsDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
