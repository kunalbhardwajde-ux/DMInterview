using System.Text;
using LmsTracker.Api;
using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.Ai;
using LmsTracker.Api.Infrastructure.Auth;
using LmsTracker.Api.Infrastructure.BackgroundTasks;
using LmsTracker.Api.Infrastructure.LinkedIn;
using LmsTracker.Api.Infrastructure.Udemy;
using LmsTracker.Api.Modules.Assignments;
using LmsTracker.Api.Modules.Courses;
using LmsTracker.Api.Modules.Departments;
using LmsTracker.Api.Modules.Integrations;
using LmsTracker.Api.Modules.Learners;
using LmsTracker.Api.Modules.Reports;
using LmsTracker.Api.Modules.Teams;
using System.Threading.RateLimiting;
using LmsTracker.Api.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<OpenApiBearerSecuritySchemeTransformer>());
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        // X-Total-Count isn't on the CORS response-header safelist, so without this the browser's
        // fetch() silently hides it on any real cross-origin request - the Vite dev proxy masks
        // this (it makes the browser's request same-origin), so it only surfaces once the UI is
        // served from anywhere else. See PagingHelper.cs / apiClient.js's apiRequestPage.
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Total-Count");
    });
});

var connectionString = builder.Configuration.GetConnectionString("LmsDb")
    ?? "Server=localhost;Database=LMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// LmsDbContext reads the current request's ClaimsPrincipal through this to attribute audit-log
// entries in SaveChangesAsync - see LmsDbContext's DescribeActor.
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<LmsDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read lazily, inside this delegate, rather than snapshotting a value beforehand: this
        // delegate only runs when JwtBearerOptions are first resolved (i.e. after the host is
        // fully built), so it always reflects the final, fully-composed configuration -
        // including test-only overrides WebApplicationFactory layers on for integration tests.
        var auth = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = auth.Issuer,
            ValidateAudience = true,
            ValidAudience = auth.Audience,
            ValidateIssuerSigningKey = true,
            // A too-short/empty key here can never actually validate a real token:
            // JwtTokenIssuer.IssueManagerToken refuses to mint one against a key this short, so
            // this fallback only prevents SymmetricSecurityKey's constructor from throwing on a
            // misconfigured deployment - it does not create a usable bypass.
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrEmpty(auth.SigningKey) ? new string('0', JwtTokenIssuer.MinimumSigningKeyBytes) : auth.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
    options.AddPolicy("LearnerOnly", policy => policy.RequireRole("Learner"));
});

builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));

// Constant-time comparison in AuthModule stops timing attacks on a single guess; this stops
// brute-forcing the credential space over the network by volume. Partitioned per client IP so
// one attacker can't exhaust a shared bucket and lock out everyone else - each IP gets its own
// window, rejected outright (QueueLimit 0) rather than queued/delayed. Limit/window read lazily
// per partition lookup (same reasoning as the JWT bearer options above) so the integration test
// factory's config override is picked up rather than a value snapshotted before it applies.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var result = ApiResult<LoginResponse>.Fail(
            "Too many sign-in attempts. Please wait a moment and try again.", "RATE_LIMITED");
        await context.HttpContext.Response.WriteAsJsonAsync(result, cancellationToken);
    };

    options.AddPolicy("login", httpContext =>
    {
        var rateLimiting = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiting.LoginPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimiting.LoginWindowSeconds),
                QueueLimit = 0,
            });
    });
});
// A default HttpClient has no timeout ceiling worth relying on (100s) - an unresponsive
// Udemy/LinkedIn endpoint would otherwise hang a request for that entire window. Combined with
// AddPolicyHandler below: the timeout guarantees a *definite* failure, and the retry+circuit-
// breaker policy handles the two things a bare timeout can't - retrying a transient blip, and
// backing off from a dependency that's genuinely down instead of retrying every request against it.
var providerCallTimeout = TimeSpan.FromSeconds(10);

builder.Services.Configure<UdemyBusinessOptions>(builder.Configuration.GetSection("UdemyBusiness"));
builder.Services.AddHttpClient<IUdemyBusinessClient, UdemyBusinessClient>(client => client.Timeout = providerCallTimeout)
    .AddPolicyHandler(ResiliencePolicies.RetryWithCircuitBreaker());
builder.Services.Configure<LinkedInLearningOptions>(builder.Configuration.GetSection("LinkedInLearning"));
builder.Services.AddHttpClient<ILinkedInLearningClient, LinkedInLearningClient>(client => client.Timeout = providerCallTimeout)
    .AddPolicyHandler(ResiliencePolicies.RetryWithCircuitBreaker());
builder.Services.Configure<AnthropicSkillTagExtractorOptions>(builder.Configuration.GetSection("AnthropicSkillTagging"));
// LLM completions run longer than a simple REST catalog lookup - a wider timeout than the
// provider clients, still bounded so a course sync can never hang indefinitely on this call.
builder.Services.AddHttpClient<ISkillTagExtractor, AnthropicSkillTagExtractor>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .AddPolicyHandler(ResiliencePolicies.RetryWithCircuitBreaker());
builder.Services.AddHostedService<SkillTagExtractionPollingService>();
builder.Services.AddEndpointModules();
builder.Services.AddSingleton<SkillMatchScoringService>();
builder.Services.AddScoped<SkillMatchReportService>();
builder.Services.AddScoped<CourseCatalogService>();
builder.Services.AddScoped<AssignmentWorkflowService>();
builder.Services.AddScoped<TeamManagementService>();
builder.Services.AddScoped<DepartmentManagementService>();
builder.Services.AddScoped<LearnerManagementService>();
builder.Services.AddScoped<ProviderSyncService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("LMS Tracker API"));
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// The "Testing" environment runs against an in-memory DbContext (see integration tests),
// which doesn't support Database.MigrateAsync() and doesn't need the demo-data seed.
if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseInitializer.InitializeAsync(app.Services);
}

app.MapEndpointModules();

app.Run();

public partial class Program;
