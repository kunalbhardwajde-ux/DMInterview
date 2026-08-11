# LMS Tracking MVP — Skill-Based Interview Priority

A 2-day interview MVP with a .NET Core backend and React frontend, built as an LMS with one specific product goal on top of it: **given a client's skill requirement, rank which internal employees are the best fit for a client interview**, using their completed-course history (synced from Udemy Business / LinkedIn Learning) as evidence.

The "Skill-Based Interview Priority" panel (manager view) is the differentiating feature — everything else (departments/teams/learners, course catalog, assignments, progress tracking) exists to produce the completion data that feature ranks against.

## Features

- Create departments, teams, and learners
- Search the course catalog and sync it from Udemy Business / LinkedIn Learning
- Assign courses to individual learners or full teams
- Mark access type as temporary or permanent
- Track progress per learner assignment
- Rank learners against a requested skill list for client interviews, with an explainable per-category score breakdown and an explicit missing-skills list
- View quick dashboard metrics and mandatory-course compliance gaps
- Two real, server-enforced login roles: Manager (broad access) and Learner (read-only, sees only their own profile and assigned courses)
- Every mutation is automatically audit-logged and viewable by a Manager

## Tech Stack

- Backend: ASP.NET Core 9 Minimal API
- Frontend: React + Vite
- Storage: SQL Server (localhost, Windows Authentication, database name `LMS`)

## Architecture

- Code-first with EF Core (entities in `Domain`, mappings in `LmsDbContext`)
- Modular monolith: each feature has its own endpoint module under `Modules/*`
- Clean separation for extraction later:
	- `Domain`: core entities and enums
	- `Infrastructure`: DB/init, cross-cutting concerns (`ApiResult`, learning-provider abstraction), and module registration
	- `Modules`: feature endpoints and feature logic

This structure is ready for future microservice extraction by moving a module and its contracts into a separate service boundary.

## Authentication

Every endpoint except `GET /api/health` and `POST /api/auth/login` requires a JWT bearer token — an unauthenticated request gets a real `401`, not just a client-side gate. `Modules/Auth/AuthModule.cs` issues tokens for **two roles**:

- **Manager** — `POST /api/auth/login` with `{ accessCode }`, compared constant-time (`CryptographicOperations.FixedTimeEquals`) against `Auth:ManagerAccessCode`. Broad access to every admin endpoint (`ManagerOnly` policy).
- **Learner** — `POST /api/auth/login` with `{ employeeCode }`, looked up (trimmed/uppercased) against the `Learners` table. Read-only access scoped strictly to that learner's own data (`LearnerOnly` policy): `GET /api/learners/me` and `GET /api/assignments/mine`, both deriving identity from the JWT's `learner_id` claim — never a client-supplied parameter, so one Learner's token can never read another's data (covered by an integration test that seeds two learners and asserts exactly that).

`LoginRequest` requires *exactly one* of `accessCode`/`employeeCode` (`IValidatableObject`, `400` otherwise). The response (`{ token, expiresAtUtc, role }`) carries a signed JWT (HMAC-SHA256, `Auth:SigningKey`) valid for `Auth:TokenLifetimeMinutes` (8 hours by default), with a `ClaimTypes.Role` claim (`"Manager"`/`"Learner"`) and, for Learner tokens, a `learner_id` claim. `POST /api/auth/login` is rate-limited per client IP (`RateLimiting:LoginPermitLimit`, default 5 per `LoginWindowSeconds`, default 60) — a `429` with a structured `ApiResult` body once exceeded, on top of the constant-time comparison that already closes the single-guess timing side-channel. Cross-role denial (a Learner token hitting a Manager-only endpoint, or vice versa) is verified with a `[Theory]` integration test that asserts `403` against every Manager-only route group individually — Departments, Teams, Learners, Courses, Assignments, Dashboard, Reports, Audit Log, and both provider-integration modules — not just one representative route standing in for the rest.

The UI (`src/components/LoginGate.jsx`) shows a role-toggle sign-in screen in real (non-mock) mode before rendering anything else — Manager (password-masked access code) or Learner (plain-text employee code; it's an identity lookup, not a secret, so masking it would only make it harder to type correctly). `apiClient.js` attaches the token to every request and bounces back to the gate automatically on a `401` via a small pub/sub (`onAuthExpired`). A real Learner session renders an entirely separate page (`features/learner/LearnerSelfService.jsx`) and never fires any of the Manager-only data-loading calls the rest of the app makes on mount (`useManagerData`'s `enabled` flag) — so a Learner token doesn't generate a wall of `403`s before that page renders. This routing decision (`authRole === 'Learner'` → `LearnerSelfService`, otherwise → the Manager UI) is itself covered by a dedicated test (`App.roleRouting.test.jsx`) asserting both branches, not just that each page works when rendered directly.

**A second, unrelated "enter an employee code" screen also exists, and is deliberately not this.** The Manager-facing Learner Portal (`/learner` route, reachable only from an authenticated Manager session) has an "Individual Persona" preview — type any employee code and see that learner's data as already loaded into the Manager's own session. It is pure client-side filtering against data the Manager already has, does zero server verification, and proves nothing about identity. It exists to let a manager preview what a learner would see, not to authenticate anyone. Both the section header and the login prompt say so explicitly in the UI (`"Learner Personas (Preview)"`, `"Preview an Employee's View"`) specifically so it doesn't get mistaken for the real Learner login above.

**Explicit scope boundary — read before assuming more than is there:** Manager access is still a single shared credential across everyone who has the access code — there is no per-manager identity, so a Manager-attributed mutation in the audit log (see below) is attributed to "Manager (session `<jti prefix>`)," which distinguishes *login sessions* from each other, not *people*. Learner access is real per-employee identity now, but deliberately **read-only** in this pass — a Learner can see their own profile and assigned courses, not update progress or reach any Manager-only endpoint; that was the explicit scope of the ask this closed ("so that they can see the assigned courses"), not a general-purpose learner self-service feature.

Configure `Auth` and `RateLimiting` in `appsettings.json` / `appsettings.Development.json` (both ship a real local-dev value, same precedent as `ConnectionStrings:LmsDb` — override both via environment variable or a secret manager for any real deployment):

- `SigningKey`: HMAC-SHA256 key, minimum 32 bytes — validated at token-issuance time (`JwtTokenIssuer.ValidateSigningKey`); a misconfigured deployment fails loudly on first login, not silently.
- `ManagerAccessCode`: the shared credential.
- `Issuer` / `Audience` / `TokenLifetimeMinutes`.
- `RateLimiting:LoginPermitLimit` / `LoginWindowSeconds`: not present in committed `appsettings.json`, so both fall back to `RateLimitingOptions`'s class defaults (5 / 60). Read lazily per rate-limit-partition lookup (same lazy-config-read trick as the JWT bearer options), so the integration test factory can override it for its shared fixture without a code change — a production-sized limit would otherwise throttle the test run itself, since the shared `IClassFixture` logs in at least once per test method.

## Audit Log

Every mutation is recorded automatically — `LmsDbContext.SaveChangesAsync` is overridden to inspect the EF change tracker on every save and write one `AuditLogEntry` row per Added/Modified/Deleted entity, in the same transaction as the save itself. No service method calls an audit logger explicitly; there's nothing to forget to instrument, and no mutation can bypass it short of writing raw SQL against the database directly.

- **Actor attribution**: a Learner token's employee code (`Learner:EMP1001`), or for Manager — a single shared credential — the token's `jti` (`Manager (session 92fc7ab6)`), which distinguishes one login session from another without pretending to identify a specific person. Seed/startup writes with no `HttpContext` (e.g. `DatabaseInitializer`) are attributed to `System`.
- **View it**: `GET /api/audit-log?page=&pageSize=` (`ManagerOnly`), newest-first, same optional server-pagination contract as every other list endpoint.
- **What this does not solve**: per-manager identity. The access code is still shared, so "who" for a Manager-attributed mutation narrows only to "whoever held a valid token during this specific login session," not a specific person — a real fix needs individual Manager accounts, which is a materially bigger feature than adding a log table, and is still an open gap (see below).

## How Skill Matching Actually Works (read this before demoing it)

`SkillMatchScoringService` (`Modules/Reports/SkillMatchScoringService.cs`) scores each learner against a requested skill list on four independent 0–5 signals (matched skills, matched courses, mandatory-course compliance, overall completion depth), summed to a 0–20 score and tiered Low/Medium/High. The API returns the full breakdown (`ScoreBreakdown`) plus which requested skills were matched *and* which were not (`MissingSkillKeywords`) — both are rendered in the UI so a recruiter sees the reasoning, not just a number.

**What this is:** a deterministic, fully tested, explainable rules engine — no LLM anywhere in the scoring/ranking path, on purpose (see below). Matching is a case-insensitive substring check of each requested skill against completed-course titles, plus (see next section) any LLM-extracted skill tags on that course. With tags disabled — the default in this repo — a request for "kubernetes" matches a course titled "Advanced Kubernetes Operations" but **not** one titled "Container Orchestration with K8s." That gap is what the optional AI tagging below exists to close.

### AI-assisted skill-tag extraction (optional, disabled by default)

`Infrastructure/Ai/AnthropicSkillTagExtractor.cs` calls the Claude Messages API (`claude-haiku-4-5` by default) once per course-catalog sync, batching every course touched by that sync into a single request, and asks it to extract 2–6 normalized skill/technology tags per course (e.g. a course titled "Container Orchestration with K8s" gets tagged `["kubernetes", "container-orchestration"]`). Tags are persisted on `Course.SkillTags` and consulted by `SkillMatchScoringService` alongside the title check, so the "K8s" example above now matches a "kubernetes" search. **The LLM never touches the score itself** — it only enriches data at sync time; ranking stays the deterministic rules engine described above, unit-tested with zero tags present.

Why this design, specifically:

- **Model choice:** `claude-haiku-4-5`, not a frontier model. Extracting tags from a course title is a bounded classification task, not open-ended reasoning — Opus-tier intelligence buys nothing here and costs 5x more per token. Model selection is itself part of the engineering judgment, not just "call an LLM."
- **Structured output, not free-text parsing:** the request sets `output_config.format` to a JSON Schema (`{"courses":[{"id","tags","confidence"}]}`, `additionalProperties: false`), so the API guarantees a parseable shape back — no regex-scraping a prose response, no "please respond with only JSON" prompt-begging.
- **Batched, not per-course, and only for what actually needs it:** every *untagged* course touched by one sync goes into a single request — already-tagged courses are skipped, so re-syncing a catalog you've synced before (the common case) doesn't re-spend an LLM call on courses whose tags haven't gone stale.
- **Off the request path, and durable:** `POST /api/courses/sync` upserts courses, flags newly-touched-and-untagged ones by setting `Course.SkillTagExtractionRequestedAtUtc`, and returns immediately — extraction itself runs later. `SkillTagExtractionPollingService` (`Infrastructure/BackgroundTasks/`) polls for flagged courses every 15s (plus once immediately on startup) and processes them in its own DI scope. This replaced an earlier in-memory `Channel<Func<...>>` queue design: that version lost anything still queued if the process crashed between "sync completed" and "extraction ran"; this one doesn't, because the pending-work marker is a column in SQL Server, not process memory — a crash just means the next poll after restart finds the same flagged rows and retries them. Verified with a test that simulates the crash directly: flag a course, discard the `DbContext`, open a fresh one against the same database, and confirm the pending flag (and the ability to finish the work) survived.
- **Confidence-gated, not trusted verbatim:** the model also self-rates each course's tags as `high`/`medium`/`low` confidence, and only `high` is auto-applied — `medium`/`low` are discarded outright rather than stored as "maybe right." This is a real but limited safeguard: self-reported LLM confidence is a heuristic, not a calibrated probability. A discarded low-confidence result is naturally retried on the course's next sync (extraction only ever targets untagged courses), not stuck.
- **Validated, not trusted verbatim (quality):** the schema guarantees *shape*, not *quality* — the response is still trimmed, lowercased, deduplicated, and capped (6 tags/course) before it ever reaches the database, because a structured-output guarantee is not a hallucination guarantee.
- **Fails open, always:** disabled by default, and any failure mode — disabled, missing API key, HTTP error, timeout, malformed response — degrades to an empty result rather than throwing. A broken or misconfigured AI call can never fail a course sync; it just means matching stays title-only, exactly like today.
- **Real retry + circuit breaker**, not just a bare timeout: `Infrastructure/ResiliencePolicies.cs` wraps this client (and Udemy/LinkedIn) in a Polly policy — 2 retries with exponential backoff on transient failures, wrapped by a circuit breaker that opens for 30s after 5 consecutive failures so a genuinely-down dependency gets failed-fast instead of retried on every request. Each client gets its own circuit-breaker instance; state is never shared across the three independent dependencies.
- **Manager override, not silent trust forever:** `DELETE /api/courses/{id}/skill-tags` (`CourseCatalogService.ClearSkillTagsAsync`) resets a course's tags to empty. Matching falls back to title-only for that course immediately, and it's picked up for re-extraction on the next sync that touches it. This is the honest scope of "human review" here — there's no approval-before-publish workflow, but a manager who spots a wrong tag has a real, working lever to remove it, and it's reachable from the Course Catalog UI itself (a "Clear tags" button next to any course carrying tags), not just via direct API calls.
- **PII:** course titles are catalog metadata, not learner data — no learner PII ever crosses the LLM boundary in this flow.
- **What's honestly not addressed:** no evaluation harness scoring extraction accuracy against a labeled set — there's no labeled ground truth to score against, and building one is separate scope from the extraction pipeline itself; course titles originate from Udemy/LinkedIn's own APIs, not end-user input, so the prompt-injection surface is limited to what a course provider could inject into its own catalog — still worth naming rather than ignoring. The UI shows tags and confidence framing (an "AI-suggested • high confidence" badge, since only high-confidence tags are ever persisted) but not a per-tag numeric confidence score, because the backend never retains one for medium/low results to show.

**Not configured in this repo:** `AnthropicSkillTagging.Enabled` is `false` and `ApiKey` is empty in both `appsettings.json` and `appsettings.Development.json` — no secret is committed, matching the existing Udemy/LinkedIn pattern. To try it, set `Enabled: true` and a real key (env var or user-secrets, never in source control) and run a course sync. The fallback path (disabled → title-only matching) is exercised end-to-end by the test suite; the live Anthropic API call itself is not, since no key is configured here.

## Udemy Business Integration

- `GET /api/courses?query=` — read-only local catalog search (`Course` table only, no external calls, no writes).
- `POST /api/courses/sync?query=` — pulls matching courses from Udemy Business (and LinkedIn Learning) and upserts them into the local catalog. Split into its own endpoint because a `GET` must have no side effects; the UI calls this before the `GET` whenever a non-empty search is submitted.
- `POST /api/integrations/udemy/sync-progress` — pulls actual learner progress from Udemy and updates assignments.

Configure `UdemyBusiness` in `appsettings.Development.json`:

- `Enabled`: true
- `BaseUrl`: your Udemy Business subdomain URL
- `ClientId` / `ClientSecret` (for basic auth mode) or `BearerToken` (if bearer mode is used)
- `CatalogSearchPathTemplate`: path with `{query}`
- `ProgressPathTemplate`: path with `{userEmail}` and `{courseExternalId}`

Note: exact path templates can vary by Udemy Business account and enabled APIs. Update these values according to your Udemy Business API documentation.

## LinkedIn Learning Integration

- `POST /api/courses/sync?query=` also syncs LinkedIn Learning catalog results when configured (same endpoint as Udemy — see above).
- `POST /api/integrations/linkedin/sync-progress` pulls actual learner progress from LinkedIn and updates assignments.

Configure `LinkedInLearning` in `appsettings.Development.json`:

- `Enabled`: true
- `BaseUrl`: LinkedIn API base URL
- `BearerToken` (recommended) or `ClientId` / `ClientSecret`
- `CatalogSearchPathTemplate`: path with `{query}`
- `ProgressPathTemplate`: path with `{userEmail}` and `{courseExternalId}`

Note: LinkedIn API response shapes and endpoints can vary by contract and permissions, so update templates for your tenant docs.

Neither integration ships with real credentials — `appsettings.Development.json` has `Enabled: false` and empty secret fields by default.

## Local Fallback Catalog

If Udemy Business API access is unavailable, the backend auto-seeds 55 local Udemy-style courses on first run.

- Search and assignment continue to work using this local catalog.
- Live progress sync still requires Udemy Business credentials and endpoints.

## Run Locally

### 1) Backend

```powershell
cd LmsTracker.Api
dotnet restore
dotnet run
```

Backend runs on `http://localhost:5000`.
Connection string is configured in `appsettings.json` and `appsettings.Development.json`:

`Server=localhost;Database=LMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`

The API auto-creates required tables and seeds starter courses on first run.

## Module Layout

- `Modules/Health`
- `Modules/Auth`
- `Modules/AuditLog`
- `Modules/Departments`
- `Modules/Courses`
- `Modules/Teams`
- `Modules/Learners`
- `Modules/Assignments`
- `Modules/Dashboard`
- `Modules/Reports`
- `Modules/Integrations`

### 2) Frontend

```powershell
cd lms-tracker-ui
npm install
npm run dev
```

Frontend runs on `http://localhost:5173` and proxies `/api` to backend.

### Frontend Mock API Toggle

Use `lms-tracker-ui/.env` to switch between real backend APIs and in-memory mock APIs.

1. Copy `lms-tracker-ui/.env.example` to `lms-tracker-ui/.env`.
2. Set `VITE_USE_MOCK_API=true` to run fully with mock APIs.
3. Set `VITE_USE_MOCK_API=false` to use real APIs via `/api`.

Mock mode preserves the same interaction flow: courses search/sync, assignment creation, progress updates, dashboard numbers, mandatory compliance, skill matching, and sync simulation actions. The mock's skill-match logic is kept in sync with `SkillMatchScoringService.cs` in both places that matter: the scoring formula (four signals, each capped at 5, summed to 0-20) and the matching predicate itself (`courseMatchesSkill` mirrors `CourseMatchesSkill`/`TagMatchesSkill` — a course matches a requested skill via its title OR any AI-extracted skill tag, not title alone), so mock and live mode rank learners identically even when a match only shows up through a tag. Mock mode never talks to a real server, so it skips the login gate entirely; real mode (`VITE_USE_MOCK_API=false`) shows a role-toggle sign-in screen first — **Manager**: the `Auth:ManagerAccessCode` value from `appsettings.Development.json` (`manager-dev-access-2026` by default); **Learner**: any seeded employee code (e.g. `EMP1001`) routes to the read-only self-service page instead of the Manager UI.

## API Endpoints (MVP)

All endpoints below require a bearer token (`Authorization: Bearer <token>`) except the two marked open. Endpoints marked `Learner` require the `LearnerOnly` policy (a Manager token gets `403`, not `401` — it's authenticated, just the wrong role); everything else marked with neither requires `ManagerOnly` (and a Learner token gets `403` there too).

- `GET /api/health` — includes DB connectivity check (open — infra/load-balancer probe)
- `POST /api/auth/login` — `{ accessCode }` or `{ employeeCode }` (exactly one) → `{ token, expiresAtUtc, role }` (open — this is how you get a token; rate-limited per client IP, see Authentication above)
- `GET /api/departments`
- `POST /api/departments`
- `GET /api/teams?page=&pageSize=&sortBy=&sortDir=` — `sortBy` is one of `name`/`department`/`managerName`/`managerEmail`, `sortDir` is `asc` (default) or `desc`
- `POST /api/teams`
- `GET /api/learners?page=&pageSize=`
- `POST /api/learners`
- `GET /api/learners/me` — **Learner** — the caller's own profile, derived from the token's `learner_id` claim
- `GET /api/courses?query=&provider=&page=&pageSize=`
- `POST /api/courses/sync?query=`
- `PATCH /api/courses/{courseId}/mandatory`
- `DELETE /api/courses/{courseId}/skill-tags` — manager override for a bad AI-extracted tag (see AI section above)
- `POST /api/assignments`
- `GET /api/assignments?page=&pageSize=`
- `GET /api/assignments/mine?page=&pageSize=&sortBy=&sortDir=` — **Learner** — the caller's own assignments only; `sortBy` is one of `courseTitle`/`provider`/`accessType`/`dueDate`/`progressPercent`/`status`
- `PATCH /api/assignments/{assignmentId}/progress`
- `GET /api/dashboard`
- `GET /api/reports/progress`
- `GET /api/reports/mandatory-compliance`
- `GET /api/reports/skill-match?skills=&top=`
- `GET /api/audit-log?page=&pageSize=` — newest-first log of every mutation (see Audit Log below)
- `GET /api/integrations/udemy/status`, `POST /api/integrations/udemy/sync-progress`
- `GET /api/integrations/linkedin/status`, `POST /api/integrations/linkedin/sync-progress`

`page`/`pageSize` are optional and backward-compatible on all list endpoints — omit them and you get the full, unbounded list exactly as before; the response sets an `X-Total-Count` header only when paging params are supplied (and `Access-Control-Expose-Headers` is set on the CORS policy specifically so a real cross-origin browser `fetch()` can actually read that header, not just a same-origin one via the Vite dev proxy). The Course Catalog and Teams Directory tables in the UI consume this for real (`apiClient.js`'s `apiRequestPage`, backed by real integration + mock-backend tests on both sides); Assignments and Mandatory Compliance still paginate client-side over a fully-fetched list, which is a fine tradeoff at today's data volume but would need the same treatment if either grew large. The mock backend's own paging/clamping logic (mirroring `PagingHelper.cs`'s contract) is one shared `applyOptionalMockPaging` helper used by every paginated mock endpoint, not copy-pasted per endpoint.

**Column sorting.** Every table in the UI has clickable, sortable column headers (`SortableHeader.jsx`, `aria-sort` + a visible ▲/▼/⇅ indicator). Client-paginated tables (Progress Tracker, Mandatory Gaps, Team Manager views, Skill Match) sort the full underlying array in the browser (`utils/sorting.js`'s `sortRows`/`useSortState`) before paginating, so the sort is always correct across the whole dataset, not just the visible page. The two server-paginated tables — Teams Directory and the Learner's own assignments (`/assignments/mine`) — get genuine server-side sorting instead: `sortBy`/`sortDir` query params reorder the query *before* it's paged (`TeamsModule.cs`/`AssignmentsModule.cs`), because sorting only the current page client-side would silently produce the wrong order once there's more than one page. The mock backend mirrors the real Teams sort exactly (`sortMockTeams` in `apiClient.js`) so mock and live mode behave identically; `/assignments/mine` has no mock handler at all since real Learner sessions are unreachable in mock mode by design.

## Scope & Tradeoffs

Deliberate cuts, kept out of scope on purpose:

- **No background jobs for provider sync.** Progress sync is triggered on-demand from the UI rather than on a schedule. Skill-tag extraction *is* a background job (durable, polling-based — see the AI section above), but a scheduled/recurring job runner for provider progress sync is separate scope.
- **AI skill-tag extraction is opt-in and unconfigured here.** See "AI-assisted skill-tag extraction" above — the architecture, durable background processing, confidence gating, UI surfacing, and fallback path are all real, tested, and live-verified end-to-end except the one thing that genuinely can't be: no API key is committed, so the live Anthropic call itself isn't exercised in this environment. There's still no accuracy-evaluation harness against a labeled set — see that section's honesty list.
- **Learner access is read-only.** A Learner can see their own profile and assigned courses; they cannot update progress or reach any Manager-only endpoint. That's the literal scope of the ask this closed, not a general-purpose self-service feature.

Known, still-open gaps (found by review, not yet fixed):

- **No per-manager identity.** Manager access is still a single shared credential — the audit log (see above) can distinguish one login *session* from another via the token's `jti`, but not one *person* from another. Closing this needs real per-manager accounts, a materially bigger feature than everything else in this list combined.
- **The live Anthropic API call is never exercised in this environment**, because no key is configured here — everything around it (extraction pipeline, confidence gating, durable queue, UI, fallback path) is real and tested against stubs, but the actual model call itself isn't. Not fixable without a credential this sandbox doesn't have.
- **No accuracy-evaluation harness** for AI-extracted skill tags against a labeled ground-truth set — there's no such set to build one against yet.
- **`useManagerData.js` (~520 lines) and `ManagerPage.jsx` (~540 lines) are large.** Each owns one coherent concern (all Manager data-loading; all Manager-page rendering) and both have real test coverage across their full surface, so this is a "worth splitting eventually" note, not a correctness problem — but it's the same shape of complaint `App.jsx` used to have before it was split, just relocated. Deliberately not touched yet: splitting either further right now is a real-risk refactor to well-tested code for a proportionally small improvement, versus the smaller, cheaper fixes elsewhere in this list.

## Notes for Interview

- Udemy Business and LinkedIn Learning both require live API access this environment doesn't have — both are `Enabled: false` with no secret committed, and the codebase demonstrates the fallback/disabled path for both (including the Polly retry/circuit-breaker policy wrapping each), not a live call. Anthropic (skill-tag extraction) is the same story — see the AI section's honesty list for exactly what is and isn't exercised.
- Authentication is real and enforced server-side (JWT bearer, `401`/`403` on every protected endpoint depending on whether the token is missing or just the wrong role) — not a UI-only gate. Two real roles now exist (Manager, Learner), not one shared credential pretending to be authorization. See the Authentication section above for exactly what it does and does not cover.
- Every mutation is audit-logged automatically (change-tracker-driven, not manually instrumented per endpoint) and viewable via `GET /api/audit-log` — see the Audit Log section above for what "actor" does and doesn't mean here.
- `POST /api/auth/login` is rate-limited per client IP, on top of the constant-time credential comparison that was already there.
- The AI skill-tag background queue is durable across a process crash (a SQL Server column, not an in-memory queue, tracks pending work) — see the AI section's "Off the request path, and durable" bullet, including how that's tested (a simulated crash-and-restart, not just "the happy path ran").
- This MVP is provider-ready and demonstrates core LMS assignment/tracking + skill-based candidate ranking workflows, with an optional AI-enrichment step that never touches the deterministic ranking logic itself, runs off the request path durably, is confidence-gated, has a manager override reachable from the UI, and is now genuinely full-stack (tags visible, override button, real pagination on two more tables).
- Add real per-manager identity and human review / accuracy evaluation of AI-extracted skill tags for production readiness — the two items left on the "known gaps" list above.
