# LMS Tracker UI

React + Vite frontend for the LMS Tracking MVP. Talks to the [`LmsTracker.Api`](../LmsTracker.Api) backend (or an in-memory mock, see below) to manage departments, teams, learners, course assignments, and progress tracking.

## What's here

Two portals, switched via the top nav (`/manager` and `/learner`):

- **LMS Manager** (`App.jsx` + `features/manager`) — create departments/teams/employees, search the course catalog and mark courses mandatory, assign courses to an individual or a whole team, edit assignment progress inline, review dashboard metrics and mandatory-compliance gaps, and trigger Udemy/LinkedIn progress sync.
- **Learner Portal** (`features/learner/LearnerPortal.jsx`) — two personas: an individual learner (sign in with an employee code, see only their own assignments) and a team manager (pick a team, see the team's progress and mandatory gaps).
- **Skill-Based Interview Priority** (`features/skillMatch`) — ranks employees against a requested skill list using a weighted score (matched skills, matched courses, mandatory-course completion, overall completion depth) computed by the backend's `SkillMatchScoringService`; results are grouped, paginated, and expandable per employee for the full skill/course breakdown.

## Architecture

- `apiClient.js` is the single seam between the UI and the backend. It exposes one `apiRequest(path, options)` function; every feature calls through it rather than touching `fetch` directly.
- `components/PaginationControls.jsx` is the one pagination UI used by every paginated table (manager tables, learner portal tables, skill-match panel) via the shared `utils/pagination.js#paginate` helper.
- `utils/errorHandling.js` normalizes whatever shape an error arrives in (`Error`, plain string, API error payload) into a single display string.

## Mock vs. real API

Copy `.env.example` to `.env` and set `VITE_USE_MOCK_API`:

- `true` — runs entirely against an in-memory mock backend built into `apiClient.js` (seeded departments/teams/learners/courses, its own validation rules, simulated Udemy/LinkedIn sync). No .NET backend required.
- `false` — proxies `/api` to the real backend at `http://localhost:5000`. Every real response is wrapped by the backend's `ApiResult` envelope (`{success, data, message, code, errors}`); `apiRequest` unwraps `data` automatically so the rest of the app works with the same plain shapes in both modes.

## Run Locally

```powershell
npm install
npm run dev
```

Runs on `http://localhost:5173`.

## Testing

```powershell
npm test
```

Covers: pagination math (`utils/pagination.test.js`), skill-match grouping/scoring-tier logic (`features/skillMatch/skillMatchUtils.test.js`), the mock API's validation/duplicate-detection rules (`apiClient.mock.test.js`), skill-match row expand/collapse (`features/skillMatch/SkillMatchPanel.test.jsx`), the shared pager component (`components/PaginationControls.test.jsx`), and an end-to-end Mandatory Course Gaps pagination flow (`App.test.jsx`).
