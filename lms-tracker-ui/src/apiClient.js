const useMockApi = String(import.meta.env.VITE_USE_MOCK_API ?? 'false').toLowerCase() === 'true'
const apiBase = '/api'
const tokenStorageKey = 'lms.authToken'
const tokenExpiryStorageKey = 'lms.authTokenExpiresAt'
const tokenRoleStorageKey = 'lms.authTokenRole'

const mockDb = createMockDb()

export const apiConfig = {
  useMockApi,
}

// Mock mode never talks to a real server, so it never needs a token - the whole auth gate is
// skipped for it (see App.jsx). Real mode requires every non-login/non-health endpoint to carry
// a bearer token (server-enforced - see AuthModule.cs); this module is the single chokepoint
// every real-mode request passes through, so token attachment and 401 handling live here once.
let authToken = useMockApi ? null : sessionStorage.getItem(tokenStorageKey)
// ISO string the backend returns alongside the token (LoginResponse.ExpiresAtUtc) - stored
// separately so useAuthSession can warn before the token dies, instead of the user's first
// signal being a generic failed request once it already has.
let tokenExpiresAt = useMockApi ? null : sessionStorage.getItem(tokenExpiryStorageKey)
// "Manager" or "Learner" (LoginResponse.Role) - drives which UI App.jsx renders after login,
// since a Learner token can't call any of the Manager-only endpoints the rest of the app uses.
let tokenRole = useMockApi ? null : sessionStorage.getItem(tokenRoleStorageKey)

export function getAuthToken() {
  return authToken
}

export function getTokenExpiresAt() {
  return tokenExpiresAt
}

export function getAuthRole() {
  return tokenRole
}

export function setAuthToken(token, expiresAt = null, role = null) {
  authToken = token
  tokenExpiresAt = expiresAt
  tokenRole = role

  if (token) {
    sessionStorage.setItem(tokenStorageKey, token)
  } else {
    sessionStorage.removeItem(tokenStorageKey)
  }

  if (expiresAt) {
    sessionStorage.setItem(tokenExpiryStorageKey, expiresAt)
  } else {
    sessionStorage.removeItem(tokenExpiryStorageKey)
  }

  if (role) {
    sessionStorage.setItem(tokenRoleStorageKey, role)
  } else {
    sessionStorage.removeItem(tokenRoleStorageKey)
  }
}

export function clearAuthToken() {
  setAuthToken(null, null, null)
}

// Raised when the server rejects the current token (missing, expired, or never set). Callers
// that want to bounce the user back to the login gate can check `error instanceof ApiAuthError`
// rather than string-matching a message.
export class ApiAuthError extends Error {}

// Lightweight pub/sub so App.jsx's login-gate state can react to a 401 discovered deep inside
// any of its many data-loading call sites, without threading a callback through every one of them.
const authExpiredListeners = new Set()

export function onAuthExpired(callback) {
  authExpiredListeners.add(callback)
  return () => authExpiredListeners.delete(callback)
}

function notifyAuthExpired() {
  authExpiredListeners.forEach((callback) => callback())
}

/// Logs in against the real API's login endpoint (see AuthModule.cs) - either the shared Manager
/// access code or a per-employee Learner code, exactly one. Not routed through apiRequest: login
/// itself has no token yet, and must never be attempted in mock mode. Returns the granted role
/// ("Manager"/"Learner") so callers can route to the right UI without a second request.
export async function login({ accessCode, employeeCode } = {}) {
  const response = await fetch(`${apiBase}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ accessCode: accessCode || null, employeeCode: employeeCode || null }),
  })

  const payload = await response.json().catch(() => ({}))

  if (!response.ok) {
    throw new Error(payload?.errors?.[0] || payload?.message || 'Sign-in failed.')
  }

  setAuthToken(payload.data.token, payload.data.expiresAtUtc, payload.data.role)
  return payload.data.role
}

async function performRequest(path, options = {}) {
  if (useMockApi) {
    const response = await mockRequest(path, options)
    const payload = await response.json()

    if (!response.ok) {
      throw new Error(payload?.message || 'Mock API request failed.')
    }

    return { data: payload, response }
  }

  const headers = {
    'Content-Type': 'application/json',
    ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
    ...(options.headers || {}),
  }

  const response = await fetch(`${apiBase}${path}`, {
    ...options,
    headers,
    signal: options.signal,
  })

  const payload = await response.text().then((text) => {
    if (!text) {
      return {}
    }

    try {
      return JSON.parse(text)
    } catch {
      return { message: text }
    }
  })

  if (!response.ok) {
    const message = payload?.errors?.[0] || payload?.message || 'API request failed.'
    if (response.status === 401) {
      // The token is gone either way (missing/expired/rejected) - clear it and notify
      // subscribers so the login gate reappears instead of silently retrying with a token the
      // server won't accept.
      clearAuthToken()
      notifyAuthExpired()
      throw new ApiAuthError(message || 'Session expired. Please sign in again.')
    }
    throw new Error(message)
  }

  // Every real endpoint responds with the ApiResult envelope: {success, data, message, code, errors}.
  // Unwrap it here so callers keep working with the same plain shapes as mock mode.
  const data = payload && Object.prototype.hasOwnProperty.call(payload, 'data') ? payload.data : payload
  return { data, response }
}

export async function apiRequest(path, options = {}) {
  const { data } = await performRequest(path, options)
  return data
}

// Consumes the backend's optional server-side paging contract (PagingHelper.cs): page/pageSize
// as query params, pre-paging total read back off the X-Total-Count response header. Only for
// call sites that render a real PaginationControls UI against a potentially large table - list
// endpoints called without page/pageSize (via plain apiRequest) keep returning everything, same
// as always.
export async function apiRequestPage(path, { page, pageSize, ...options } = {}) {
  const separator = path.includes('?') ? '&' : '?'
  const pagedPath = `${path}${separator}page=${page}&pageSize=${pageSize}`

  const { data, response } = await performRequest(pagedPath, options)
  const items = Array.isArray(data) ? data : []
  const totalCountHeader = response.headers.get('X-Total-Count')
  const totalCount = totalCountHeader !== null && totalCountHeader !== undefined ? Number(totalCountHeader) : items.length

  return { items, totalCount }
}

function createMockDb() {
  const now = new Date().toISOString()

  const departments = [
    { id: 'd-eng', name: 'Engineering', code: 'ENG', createdAtUtc: now },
    { id: 'd-dai', name: 'Data & AI', code: 'DAI', createdAtUtc: now },
    { id: 'd-prd', name: 'Product & Design', code: 'PRD', createdAtUtc: now },
    { id: 'd-sec', name: 'Security & Risk', code: 'SEC', createdAtUtc: now },
    { id: 'd-ops', name: 'Operations', code: 'OPS', createdAtUtc: now },
    { id: 'd-cx', name: 'Customer Excellence', code: 'CEX', createdAtUtc: now },
  ]

  const teams = [
    { id: 't-platform', departmentId: 'd-eng', name: 'Platform', managerName: 'Ananya Rao', managerEmail: 'ananya.rao@company.local', createdAtUtc: now },
    { id: 't-app', departmentId: 'd-eng', name: 'App Engineering', managerName: 'Rohit Mehta', managerEmail: 'rohit.mehta@company.local', createdAtUtc: now },
    { id: 't-ml', departmentId: 'd-dai', name: 'ML Engineering', managerName: 'Priya Singh', managerEmail: 'priya.singh@company.local', createdAtUtc: now },
    { id: 't-analytics', departmentId: 'd-dai', name: 'Analytics', managerName: 'Vikas Nair', managerEmail: 'vikas.nair@company.local', createdAtUtc: now },
    { id: 't-product', departmentId: 'd-prd', name: 'Product Strategy', managerName: 'Sana Khan', managerEmail: 'sana.khan@company.local', createdAtUtc: now },
    { id: 't-ux', departmentId: 'd-prd', name: 'UX Design', managerName: 'Arjun Das', managerEmail: 'arjun.das@company.local', createdAtUtc: now },
    { id: 't-appsec', departmentId: 'd-sec', name: 'Application Security', managerName: 'Farhan Ali', managerEmail: 'farhan.ali@company.local', createdAtUtc: now },
    { id: 't-grc', departmentId: 'd-sec', name: 'Governance & Compliance', managerName: 'Ira Sen', managerEmail: 'ira.sen@company.local', createdAtUtc: now },
    { id: 't-itops', departmentId: 'd-ops', name: 'IT Operations', managerName: 'Nitin Bhat', managerEmail: 'nitin.bhat@company.local', createdAtUtc: now },
    { id: 't-fieldops', departmentId: 'd-ops', name: 'Field Operations', managerName: 'Leena George', managerEmail: 'leena.george@company.local', createdAtUtc: now },
    { id: 't-csm', departmentId: 'd-cx', name: 'Customer Success', managerName: 'Raghav Anand', managerEmail: 'raghav.anand@company.local', createdAtUtc: now },
    { id: 't-support', departmentId: 'd-cx', name: 'Customer Support', managerName: 'Mitali Shah', managerEmail: 'mitali.shah@company.local', createdAtUtc: now },
  ]

  const firstNames = [
    'Aman', 'Neha', 'Kabir', 'Kushagra', 'Pooja', 'Harsh', 'Riya', 'Nikhil', 'Tanya', 'Rahul',
    'Dev', 'Anika', 'Meera', 'Karan', 'Sonia', 'Aditya', 'Shreya', 'Aditi', 'Omkar', 'Maya',
    'Ishan', 'Naina', 'Varun', 'Sakshi', 'Dhruv', 'Tanvi', 'Yash', 'Ira', 'Gaurav', 'Simran',
    'Parth', 'Rhea', 'Nitin', 'Aarav', 'Diya', 'Manav', 'Kriti', 'Rohan', 'Mihir', 'Pallavi',
  ]
  const lastNames = [
    'Verma', 'Iyer', 'Sharma', 'Jain', 'Menon', 'Patel', 'Bhatt', 'Gupta', 'Sethi', 'Malhotra',
    'Suri', 'Joshi', 'Ahuja', 'Paul', 'Kulkarni', 'Nanda', 'Roy', 'Deshmukh', 'Reddy', 'Kapoor',
    'Khanna', 'Saxena', 'Bose', 'Chopra', 'Mishra', 'Trivedi', 'Dubey', 'Srivastava', 'Naidu', 'Arora',
  ]
  const designations = [
    'Software Engineer', 'Senior Software Engineer', 'Lead Engineer', 'Product Analyst', 'Product Manager',
    'Data Analyst', 'Data Scientist', 'ML Engineer', 'DevOps Engineer', 'QA Engineer', 'Security Engineer',
    'Operations Specialist', 'Customer Success Manager', 'Support Engineer', 'UX Designer',
  ]

  const learners = []
  let employeeCounter = 1001
  for (let teamIndex = 0; teamIndex < teams.length; teamIndex += 1) {
    for (let slot = 0; slot < 8; slot += 1) {
      const nameSeed = teamIndex * 8 + slot
      const first = firstNames[nameSeed % firstNames.length]
      const last = lastNames[(nameSeed * 3) % lastNames.length]
      const fullName = `${first} ${last}`
      const email = `${first.toLowerCase()}.${last.toLowerCase()}.${employeeCounter}@company.local`
      const designation = designations[(nameSeed * 5) % designations.length]

      learners.push(
        makeLearner(
          `EMP${employeeCounter}`,
          fullName,
          email,
          designation,
          teams[teamIndex].id,
        ),
      )

      employeeCounter += 1
    }
  }

  const courseTracks = ['Leadership', 'Cloud', 'Security', 'Data', 'Frontend', 'Backend', 'AI', 'Compliance', 'Communication', 'Productivity']
  // A subset carries skillTags to mirror the real backend's confidence-gated AI extraction -
  // only high-confidence tags ever get persisted there, so not every course is tagged. Mirroring
  // that "some tagged, some not" mix here is what makes the skill-tags UI demonstrable in mock mode.
  const courses = Array.from({ length: 75 }, (_, index) => {
    const id = `c-${index + 1}`
    const provider = index % 2 === 0 ? 'Udemy' : 'LinkedIn'
    const track = courseTracks[index % courseTracks.length]
    return {
      id,
      externalCourseId: `EXT-${index + 1}`,
      title: `${track} Essentials ${index + 1} (${provider})`,
      provider,
      launchUrl: `https://learning.example.com/${id}`,
      isMandatory: index < 6,
      skillTags: index % 3 === 0 ? [] : [track.toLowerCase(), provider.toLowerCase()],
    }
  })

  const assignments = []
  const accessTypes = ['Temporary', 'Permanent']
  for (let i = 0; i < learners.length; i += 1) {
    const count = 2 + (i % 5)
    for (let j = 0; j < count; j += 1) {
      const course = courses[(i * 2 + j * 7) % courses.length]
      const progress = [0, 15, 30, 55, 80, 100][(i + j) % 6]
      const dayOffset = (i + j) % 8 === 0 ? -1 * (2 + ((i + j) % 9)) : 7 + ((i + j) % 28)
      assignments.push({
        id: cryptoRandomId('a'),
        learnerId: learners[i].id,
        teamId: learners[i].teamId,
        courseId: course.id,
        accessType: accessTypes[(i + j) % accessTypes.length],
        dueDate: toDateOnly(addDays(new Date(), dayOffset)),
        progressPercent: progress,
        status: toStatus(progress),
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      })
    }
  }

  return {
    departments,
    teams,
    learners,
    courses,
    assignments,
  }
}

async function mockRequest(path, options) {
  const method = (options.method || 'GET').toUpperCase()
  const url = new URL(path, 'http://mock.local')
  const body = parseBody(options.body)

  if (method === 'GET' && url.pathname === '/departments') {
    return ok(sorted(mockDb.departments, (x) => x.name).map((d) => ({ id: d.id, name: d.name, code: d.code })))
  }

  if (method === 'POST' && url.pathname === '/departments') {
    if (!body?.name?.trim() || !body?.code?.trim()) {
      return bad('Department name and code are required.')
    }

    const code = body.code.trim().toUpperCase()
    if (mockDb.departments.some((d) => d.code === code)) {
      return bad('Department code already exists.')
    }

    const item = { id: cryptoRandomId('d'), name: body.name.trim(), code, createdAtUtc: new Date().toISOString() }
    mockDb.departments.push(item)
    return ok({ id: item.id, name: item.name, code: item.code })
  }

  if (method === 'GET' && url.pathname === '/teams') {
    const rows = mockDb.teams.map((t) => {
      const d = mockDb.departments.find((x) => x.id === t.departmentId)
      return {
        id: t.id,
        name: t.name,
        departmentId: t.departmentId,
        departmentName: d?.name || 'Unknown Department',
        managerName: t.managerName,
        managerEmail: t.managerEmail,
      }
    })

    const sortedRows = sortMockTeams(rows, url.searchParams.get('sortBy'), url.searchParams.get('sortDir'))
    return applyOptionalMockPaging(sortedRows, url)
  }

  if (method === 'POST' && url.pathname === '/teams') {
    if (!body?.name?.trim() || !body?.managerName?.trim() || !body?.managerEmail?.trim()) {
      return bad('Team name, manager name, and manager email are required.')
    }

    const department = mockDb.departments.find((d) => d.id === body.departmentId)
    if (!department) {
      return bad('Department not found.')
    }

    const item = {
      id: cryptoRandomId('t'),
      departmentId: body.departmentId,
      name: body.name.trim(),
      managerName: body.managerName.trim(),
      managerEmail: body.managerEmail.trim(),
      createdAtUtc: new Date().toISOString(),
    }

    mockDb.teams.push(item)

    return ok({
      id: item.id,
      name: item.name,
      departmentId: item.departmentId,
      departmentName: department.name,
      managerName: item.managerName,
      managerEmail: item.managerEmail,
    })
  }

  if (method === 'GET' && url.pathname === '/learners') {
    return ok(buildLearnerViews(mockDb.learners))
  }

  if (method === 'POST' && url.pathname === '/learners') {
    if (!body?.employeeCode?.trim() || !body?.name?.trim() || !body?.email?.trim() || !body?.designation?.trim()) {
      return bad('Employee code, name, email, and designation are required.')
    }

    if (body.teamId && !mockDb.teams.some((t) => t.id === body.teamId)) {
      return bad('Team not found.')
    }

    const code = body.employeeCode.trim().toUpperCase()
    const email = body.email.trim().toLowerCase()
    if (mockDb.learners.some((x) => x.employeeCode === code || x.email.toLowerCase() === email)) {
      return bad('A learner with this email or employee code already exists.')
    }

    const learner = makeLearner(code, body.name.trim(), email, body.designation.trim(), body.teamId || null)
    mockDb.learners.push(learner)

    const requiredCourses = mockDb.courses.filter((c) => c.isMandatory)
    const seed = requiredCourses.length > 0 ? requiredCourses : [mockDb.courses[0]].filter(Boolean)
    for (const course of seed) {
      mockDb.assignments.push(makeAssignment(learner, course, 0))
    }

    return ok(learner)
  }

  if (method === 'GET' && url.pathname === '/courses') {
    const query = (url.searchParams.get('query') || '').toLowerCase().trim()
    const provider = (url.searchParams.get('provider') || '').trim()

    let rows = [...mockDb.courses]
    if (provider) {
      rows = rows.filter((c) => c.provider === provider)
    }
    if (query) {
      rows = rows.filter((c) => c.title.toLowerCase().includes(query))
    }

    rows.sort((a, b) => a.title.localeCompare(b.title))

    return applyOptionalMockPaging(rows, url)
  }

  if (method === 'POST' && url.pathname === '/courses/sync') {
    // Mock mode never talks to real providers, so this is a no-op kept only so the UI's
    // sync-then-search call sequence works identically in both mock and live mode.
    return ok({ synced: true })
  }

  if (method === 'PATCH' && /^\/courses\/.+\/mandatory$/.test(url.pathname)) {
    const courseId = url.pathname.split('/')[2]
    const course = mockDb.courses.find((c) => c.id === courseId)
    if (!course) {
      return notFound('Course not found.')
    }

    course.isMandatory = !!body?.isMandatory
    return ok({ id: course.id, title: course.title, isMandatory: course.isMandatory })
  }

  if (method === 'DELETE' && /^\/courses\/.+\/skill-tags$/.test(url.pathname)) {
    const courseId = url.pathname.split('/')[2]
    const course = mockDb.courses.find((c) => c.id === courseId)
    if (!course) {
      return notFound('Course not found.')
    }

    course.skillTags = []
    return ok({ id: course.id, title: course.title, skillTags: course.skillTags })
  }

  if (method === 'POST' && url.pathname === '/assignments') {
    const course = mockDb.courses.find((c) => c.id === body?.courseId)
    if (!course) {
      return bad('Course not found.')
    }

    const accessType = String(body?.accessType || '')
    if (accessType !== 'Temporary' && accessType !== 'Permanent') {
      return bad('Access type must be Temporary or Permanent.')
    }

    if (accessType === 'Temporary' && !body?.dueDate) {
      return bad('Due date is required for temporary access.')
    }

    let targets = []
    if (body?.learnerId) {
      const learner = mockDb.learners.find((l) => l.id === body.learnerId)
      if (learner) {
        targets = [learner]
      }
    }

    if (body?.teamId) {
      targets = mockDb.learners.filter((l) => l.teamId === body.teamId)
    }

    if (targets.length === 0) {
      return bad('No learners found for the selected target.')
    }

    let added = 0
    for (const learner of targets) {
      const exists = mockDb.assignments.some((a) => a.learnerId === learner.id && a.courseId === course.id)
      if (exists) {
        continue
      }

      mockDb.assignments.push({
        id: cryptoRandomId('a'),
        learnerId: learner.id,
        teamId: learner.teamId,
        courseId: course.id,
        accessType,
        dueDate: accessType === 'Temporary' ? body.dueDate : null,
        progressPercent: 0,
        status: 'NotStarted',
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      })
      added += 1
    }

    return ok({ assigned: added })
  }

  if (method === 'GET' && url.pathname === '/assignments') {
    const rows = buildAssignmentViews(filteredAssignments(url))
    return ok(rows)
  }

  if (method === 'PATCH' && /^\/assignments\/.+\/progress$/.test(url.pathname)) {
    const assignmentId = url.pathname.split('/')[2]
    const assignment = mockDb.assignments.find((a) => a.id === assignmentId)
    if (!assignment) {
      return notFound('Assignment not found.')
    }

    const progress = clamp(Number(body?.progressPercent ?? 0), 0, 100)
    assignment.progressPercent = progress
    assignment.status = toStatus(progress)
    assignment.updatedAtUtc = new Date().toISOString()

    return ok({ id: assignment.id, progressPercent: assignment.progressPercent, status: assignment.status })
  }

  if (method === 'GET' && url.pathname === '/dashboard') {
    return ok(buildDashboard(url))
  }

  if (method === 'GET' && url.pathname === '/reports/mandatory-compliance') {
    return ok(buildMandatoryCompliance(url))
  }

  if (method === 'GET' && url.pathname === '/reports/skill-match') {
    return ok(buildSkillMatch(url))
  }

  if (method === 'POST' && (url.pathname === '/integrations/udemy/sync-progress' || url.pathname === '/integrations/linkedin/sync-progress')) {
    let updated = 0
    for (const assignment of mockDb.assignments) {
      if (Math.random() < 0.35) {
        const bump = 10 + Math.floor(Math.random() * 25)
        assignment.progressPercent = clamp(assignment.progressPercent + bump, 0, 100)
        assignment.status = toStatus(assignment.progressPercent)
        assignment.updatedAtUtc = new Date().toISOString()
        updated += 1
      }
    }

    return ok({ assignmentsChecked: mockDb.assignments.length, updated })
  }

  return notFound('Mock endpoint not implemented.')
}

function filteredAssignments(url) {
  const departmentId = url.searchParams.get('departmentId') || ''
  const teamId = url.searchParams.get('teamId') || ''

  const filteredLearnerIds = new Set(
    mockDb.learners
      .filter((l) => {
        const team = mockDb.teams.find((t) => t.id === l.teamId)
        if (departmentId && team?.departmentId !== departmentId) {
          return false
        }
        if (teamId && l.teamId !== teamId) {
          return false
        }
        return true
      })
      .map((l) => l.id),
  )

  return mockDb.assignments
    .filter((a) => filteredLearnerIds.has(a.learnerId))
    .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))
}

function buildDashboard(url) {
  const rows = filteredAssignments(url)
  const assignmentCount = rows.length
  const completed = rows.filter((x) => x.status === 'Completed').length
  const inProgress = rows.filter((x) => x.status === 'InProgress').length
  const notStarted = rows.filter((x) => x.status === 'NotStarted').length

  const now = toDateOnly(new Date())
  const overdue = rows.filter((x) => x.dueDate && x.dueDate < now && x.status !== 'Completed').length

  const departmentId = url.searchParams.get('departmentId') || ''
  const teamId = url.searchParams.get('teamId') || ''

  const scopedTeams = mockDb.teams.filter((t) => {
    if (departmentId && t.departmentId !== departmentId) {
      return false
    }
    if (teamId && t.id !== teamId) {
      return false
    }
    return true
  })

  const scopedLearners = mockDb.learners.filter((l) => {
    const team = mockDb.teams.find((t) => t.id === l.teamId)
    if (departmentId && team?.departmentId !== departmentId) {
      return false
    }
    if (teamId && l.teamId !== teamId) {
      return false
    }
    return true
  })

  const scopedDepartments = departmentId
    ? mockDb.departments.filter((d) => d.id === departmentId)
    : mockDb.departments

  return {
    departments: scopedDepartments.length,
    teams: scopedTeams.length,
    learners: scopedLearners.length,
    assignments: assignmentCount,
    completed,
    inProgress,
    notStarted,
    overdue,
    completionRate: assignmentCount === 0 ? 0 : Math.round((completed * 1000) / assignmentCount) / 10,
  }
}

function buildMandatoryCompliance(url) {
  const mandatoryCourses = mockDb.courses.filter((c) => c.isMandatory)
  if (mandatoryCourses.length === 0) {
    return []
  }

  const departmentId = url.searchParams.get('departmentId') || ''
  const teamId = url.searchParams.get('teamId') || ''

  const scopedLearners = mockDb.learners.filter((l) => {
    const team = mockDb.teams.find((t) => t.id === l.teamId)
    if (departmentId && team?.departmentId !== departmentId) {
      return false
    }
    if (teamId && l.teamId !== teamId) {
      return false
    }
    return true
  })

  return scopedLearners
    .map((learner) => {
      const team = mockDb.teams.find((t) => t.id === learner.teamId)
      const department = mockDb.departments.find((d) => d.id === team?.departmentId)
      const pendingTitles = mandatoryCourses.filter((course) => {
        const assignment = mockDb.assignments.find((a) => a.learnerId === learner.id && a.courseId === course.id)
        return !assignment || assignment.status !== 'Completed'
      })

      return {
        learnerId: learner.id,
        employeeCode: learner.employeeCode,
        learnerName: learner.name,
        departmentName: department?.name || null,
        teamName: team?.name || null,
        mandatoryCourses: mandatoryCourses.length,
        completedMandatoryCourses: mandatoryCourses.length - pendingTitles.length,
        pendingMandatoryCourses: pendingTitles.length,
        pendingCourseTitles: pendingTitles.map((x) => x.title).join(', '),
      }
    })
    .filter((x) => x.pendingMandatoryCourses > 0)
    .sort((a, b) => b.pendingMandatoryCourses - a.pendingMandatoryCourses || a.learnerName.localeCompare(b.learnerName))
}

// Mirrors SkillMatchScoringService.cs's CourseMatchesSkill/TagMatchesSkill exactly: a course
// matches a requested skill if the title contains it OR any AI-extracted skill tag matches it
// (case-insensitive, either substring direction). Title-only matching here would silently miss
// every course whose skill relevance only shows up via its tags, not its title.
export function courseMatchesSkill(course, skill) {
  if ((course.title || '').toLowerCase().includes(skill)) {
    return true
  }
  return (course.skillTags || []).some((tag) => tagMatchesSkill(tag, skill))
}

function tagMatchesSkill(tag, skill) {
  if (!tag) {
    return false
  }
  const normalizedTag = tag.toLowerCase()
  return normalizedTag.includes(skill) || skill.includes(normalizedTag)
}

function buildSkillMatch(url) {
  const departmentId = url.searchParams.get('departmentId') || ''
  const teamId = url.searchParams.get('teamId') || ''
  const maxRows = clamp(Number(url.searchParams.get('top') || 30), 1, 200)
  const requestedSkills = (url.searchParams.get('skills') || '')
    .split(',')
    .map((x) => x.trim().toLowerCase())
    .filter(Boolean)

  if (requestedSkills.length === 0) {
    return []
  }

  const scopedLearners = mockDb.learners.filter((learner) => {
    const team = mockDb.teams.find((t) => t.id === learner.teamId)
    if (departmentId && team?.departmentId !== departmentId) {
      return false
    }
    if (teamId && learner.teamId !== teamId) {
      return false
    }
    return true
  })

  const rows = scopedLearners
    .map((learner) => {
      const team = mockDb.teams.find((t) => t.id === learner.teamId)
      const department = mockDb.departments.find((d) => d.id === team?.departmentId)

      const completedAssignments = mockDb.assignments.filter(
        (a) => a.learnerId === learner.id && a.status === 'Completed',
      )

      const completedCourses = completedAssignments
        .map((assignment) => mockDb.courses.find((course) => course.id === assignment.courseId))
        .filter(Boolean)

      const matchedCourses = completedCourses.filter((course) =>
        requestedSkills.some((skill) => courseMatchesSkill(course, skill)),
      )

      const matchedCourseTitles = [...new Set(matchedCourses.map((course) => course.title))]
      const matchedSkills = requestedSkills.filter((skill) =>
        completedCourses.some((course) => courseMatchesSkill(course, skill)),
      )
      const missingSkills = requestedSkills.filter((skill) => !matchedSkills.includes(skill))

      const completedMandatoryCourses = completedCourses.filter((course) => course.isMandatory).length
      const completedCourseCount = new Set(completedCourses.map((course) => course.id)).size
      const matchedCourseCount = matchedCourseTitles.length

      // Mirrors LmsTracker.Api/Modules/Reports/SkillMatchScoringService.cs exactly: four
      // signals, each capped at 5 so no one signal dominates, summed to a 0-20 score - kept in
      // sync so mock mode and the real API rank learners the same way.
      const MAX_PER_CATEGORY = 5
      const matchedSkillScore = Math.min(matchedSkills.length, MAX_PER_CATEGORY)
      const matchedCourseScore = Math.min(matchedCourseCount, MAX_PER_CATEGORY)
      const mandatoryCourseScore = Math.min(completedMandatoryCourses, MAX_PER_CATEGORY)
      const completedCourseScore = Math.min(completedCourseCount, MAX_PER_CATEGORY)

      const matchScore = matchedSkillScore + matchedCourseScore + mandatoryCourseScore + completedCourseScore

      let priorityTier = 'Low'
      if (matchScore >= 15) {
        priorityTier = 'High'
      } else if (matchScore >= 10) {
        priorityTier = 'Medium'
      }

      return {
        learnerId: learner.id,
        employeeCode: learner.employeeCode,
        learnerName: learner.name,
        departmentName: department?.name || null,
        teamName: team?.name || null,
        completedCourses: completedCourseCount,
        matchedCourses: matchedCourseCount,
        matchedSkills: matchedSkills.length,
        matchScore,
        priorityTier,
        matchedSkillKeywords: matchedSkills.join(', '),
        missingSkillKeywords: missingSkills.join(', '),
        topMatchedCourses: matchedCourseTitles.slice(0, 3).join(' | '),
        scoreBreakdown: [
          `Skills ${matchedSkillScore}/${MAX_PER_CATEGORY}`,
          `Courses ${matchedCourseScore}/${MAX_PER_CATEGORY}`,
          `Mandatory ${mandatoryCourseScore}/${MAX_PER_CATEGORY}`,
          `Depth ${completedCourseScore}/${MAX_PER_CATEGORY}`,
        ].join(', '),
      }
    })
    .sort((a, b) => {
      if (b.matchScore !== a.matchScore) {
        return b.matchScore - a.matchScore
      }
      if (b.matchedCourses !== a.matchedCourses) {
        return b.matchedCourses - a.matchedCourses
      }
      if (b.completedCourses !== a.completedCourses) {
        return b.completedCourses - a.completedCourses
      }
      return a.learnerName.localeCompare(b.learnerName)
    })

  return rows.slice(0, maxRows)
}

function buildLearnerViews(learners) {
  return learners
    .map((learner) => {
      const team = mockDb.teams.find((x) => x.id === learner.teamId)
      const department = mockDb.departments.find((x) => x.id === team?.departmentId)
      return {
        id: learner.id,
        employeeCode: learner.employeeCode,
        name: learner.name,
        email: learner.email,
        designation: learner.designation,
        teamId: learner.teamId,
        teamName: team?.name || null,
        departmentId: department?.id || null,
        departmentName: department?.name || null,
      }
    })
    .sort((a, b) => a.name.localeCompare(b.name))
}

function buildAssignmentViews(items) {
  return items.map((assignment) => {
    const learner = mockDb.learners.find((x) => x.id === assignment.learnerId)
    const course = mockDb.courses.find((x) => x.id === assignment.courseId)
    const team = mockDb.teams.find((x) => x.id === learner?.teamId)
    const department = mockDb.departments.find((x) => x.id === team?.departmentId)

    return {
      id: assignment.id,
      learnerId: assignment.learnerId,
      employeeCode: learner?.employeeCode || 'NA',
      learnerName: learner?.name || 'Unknown Learner',
      departmentId: department?.id || null,
      departmentName: department?.name || null,
      teamName: team?.name || null,
      courseId: assignment.courseId,
      courseTitle: course?.title || 'Unknown Course',
      provider: course?.provider || 'Unknown Provider',
      launchUrl: course?.launchUrl || '#',
      accessType: assignment.accessType,
      dueDate: assignment.dueDate,
      progressPercent: assignment.progressPercent,
      status: assignment.status,
    }
  })
}

function makeLearner(employeeCode, name, email, designation, teamId) {
  return {
    id: cryptoRandomId('l'),
    employeeCode,
    name,
    email,
    designation,
    teamId,
    createdAtUtc: new Date().toISOString(),
  }
}

function makeAssignment(learner, course, statusSeed) {
  const progress = [0, 45, 100][statusSeed % 3]
  return {
    id: cryptoRandomId('a'),
    learnerId: learner.id,
    teamId: learner.teamId,
    courseId: course.id,
    accessType: 'Temporary',
    dueDate: toDateOnly(addDays(new Date(), 25)),
    progressPercent: progress,
    status: toStatus(progress),
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString(),
  }
}

function parseBody(body) {
  if (!body) {
    return null
  }

  if (typeof body === 'string') {
    try {
      return JSON.parse(body)
    } catch {
      return null
    }
  }

  return body
}

function addDays(date, days) {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}

function toDateOnly(date) {
  return date.toISOString().slice(0, 10)
}

function toStatus(progress) {
  if (progress <= 0) {
    return 'NotStarted'
  }
  if (progress >= 100) {
    return 'Completed'
  }
  return 'InProgress'
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value))
}

function sorted(items, keySelector) {
  return [...items].sort((a, b) => keySelector(a).localeCompare(keySelector(b)))
}

function cryptoRandomId(prefix) {
  const random = Math.random().toString(36).slice(2, 10)
  return `${prefix}-${random}`
}

function ok(data, extraHeaders) {
  const headerEntries = extraHeaders ? Object.entries(extraHeaders) : []
  return Promise.resolve({
    ok: true,
    status: 200,
    json: async () => data,
    headers: { get: (name) => headerEntries.find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] ?? null },
  })
}

// Mirrors PagingHelper.cs's optional server-side paging contract: omitting both params returns
// everything with no header (used by dropdowns/filters that need the full list, e.g. the
// assign-course select), supplying either pages the result and reports the pre-paging total via
// X-Total-Count. Shared by every mock list endpoint that supports paging (currently /courses and
// /teams) so the clamping/slicing logic exists in exactly one place, not one per endpoint.
// Mirrors TeamsModule.cs's ApplySort exactly (same sortBy keys, same "desc" flag, same default
// department-then-name ordering) so mock mode and live mode return identical row order for the
// same query params.
function sortMockTeams(rows, sortBy, sortDir) {
  const descending = (sortDir || '').toLowerCase() === 'desc'
  const compare = (a, b) => String(a).localeCompare(String(b), undefined, { sensitivity: 'base' })
  const byKey = (key) => [...rows].sort((a, b) => (descending ? compare(b[key], a[key]) : compare(a[key], b[key])))
  const byDepartmentThenName = (direction) =>
    [...rows].sort((a, b) => {
      const deptCompare = compare(a.departmentName, b.departmentName) * direction
      return deptCompare !== 0 ? deptCompare : compare(a.name, b.name) * direction
    })

  switch ((sortBy || '').toLowerCase()) {
    case 'name':
      return byKey('name')
    case 'managername':
      return byKey('managerName')
    case 'manageremail':
      return byKey('managerEmail')
    case 'department':
    case 'departmentname':
      return byDepartmentThenName(descending ? -1 : 1)
    default:
      return byDepartmentThenName(1)
  }
}

function applyOptionalMockPaging(rows, url) {
  const pageParam = url.searchParams.get('page')
  const pageSizeParam = url.searchParams.get('pageSize')

  if (pageParam === null && pageSizeParam === null) {
    return ok(rows)
  }

  const totalCount = rows.length
  const effectivePageSize = Math.min(Math.max(Number(pageSizeParam) || 25, 1), 200)
  const effectivePage = Math.max(Number(pageParam) || 1, 1)
  const startIndex = (effectivePage - 1) * effectivePageSize
  const paged = rows.slice(startIndex, startIndex + effectivePageSize)

  return ok(paged, { 'X-Total-Count': String(totalCount) })
}

function bad(message) {
  return Promise.resolve({
    ok: false,
    status: 400,
    json: async () => ({ message }),
    headers: { get: () => null },
  })
}

function notFound(message) {
  return Promise.resolve({
    ok: false,
    status: 404,
    json: async () => ({ message }),
    headers: { get: () => null },
  })
}
