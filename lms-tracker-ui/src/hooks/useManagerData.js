import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiRequest, apiRequestPage } from '../apiClient'
import { paginate } from '../utils/pagination'
import { getErrorMessage, buildQueryString } from '../utils/errorHandling'

const COURSE_PAGE_SIZE = 8
const TEAMS_DIRECTORY_PAGE_SIZE = 8

const INITIAL_DEPARTMENT_FORM = { name: '', code: '' }
const INITIAL_TEAM_FORM = { name: '', departmentId: '', managerName: '', managerEmail: '' }
const INITIAL_LEARNER_FORM = { employeeCode: '', name: '', email: '', designation: '', teamId: '' }
const INITIAL_ASSIGN_FORM = {
  courseId: '',
  targetType: 'individual',
  learnerId: '',
  teamId: '',
  accessType: 'Temporary',
  dueDate: '',
}

// Owns every piece of state and every API call the Manager route needs: org data (departments/
// teams/learners), the course catalog, assignments + progress, the mandatory-compliance and
// skill-match reports, and the create-department/team/learner/assignment forms + modals. This is
// intentionally one hook rather than several - almost every handler here ends by re-running
// loadDashboardData, so splitting further would just move the coupling into extra parameters
// instead of removing it.
export function useManagerData({ setError, enabled = true }) {
  const { t } = useTranslation()

  const [departments, setDepartments] = useState([])
  // Two separate views over teams, same pattern as courses below: `teams` is the full, unpaged
  // list every dropdown/filter needs (scope filter, assign-form team select, learner-form team
  // options), `teamsDirectoryRows` is the current page of the dedicated, server-paginated Teams
  // Directory table.
  const [teams, setTeams] = useState([])
  const [teamsDirectoryRows, setTeamsDirectoryRows] = useState([])
  const [teamsDirectoryTotalCount, setTeamsDirectoryTotalCount] = useState(0)
  const [teamsDirectoryPage, setTeamsDirectoryPage] = useState(1)
  const [learners, setLearners] = useState([])
  // Two separate views over the same catalog: `courses` is the current page of the searchable
  // Course Catalog table (server-paginated - see searchCourses), `allCourses` is the full,
  // unpaged list used only to populate the Assign-Course dropdown, which needs every course
  // visible at once rather than whatever 8 happen to be on the catalog table's current page.
  const [courses, setCourses] = useState([])
  const [coursesTotalCount, setCoursesTotalCount] = useState(0)
  const [allCourses, setAllCourses] = useState([])
  const [assignments, setAssignments] = useState([])
  const [mandatoryComplianceRows, setMandatoryComplianceRows] = useState([])
  const [skillMatchRows, setSkillMatchRows] = useState([])
  const [skillMatchLoading, setSkillMatchLoading] = useState(false)
  const [dashboard, setDashboard] = useState(null)
  const [pendingAction, setPendingAction] = useState('')

  const [courseQuery, setCourseQuery] = useState('')
  const [scopeDepartmentId, setScopeDepartmentId] = useState('')
  const [scopeTeamId, setScopeTeamId] = useState('')
  const [coursePage, setCoursePage] = useState(1)
  const [managerAssignmentPage, setManagerAssignmentPage] = useState(1)
  const [mandatoryGapPage, setMandatoryGapPage] = useState(1)

  const [modalOpen, setModalOpen] = useState(null)
  const [departmentForm, setDepartmentForm] = useState(INITIAL_DEPARTMENT_FORM)
  const [teamForm, setTeamForm] = useState(INITIAL_TEAM_FORM)
  const [learnerForm, setLearnerForm] = useState(INITIAL_LEARNER_FORM)
  const [assignForm, setAssignForm] = useState(INITIAL_ASSIGN_FORM)
  const [progressEdits, setProgressEdits] = useState({})

  const scopeQuery = useMemo(() => {
    const searchParams = new URLSearchParams()
    if (scopeDepartmentId) {
      searchParams.set('departmentId', scopeDepartmentId)
    }
    if (scopeTeamId) {
      searchParams.set('teamId', scopeTeamId)
    }

    const query = searchParams.toString()
    return query ? `?${query}` : ''
  }, [scopeDepartmentId, scopeTeamId])

  const filteredTeamsByScope = useMemo(() => {
    if (!scopeDepartmentId) {
      return teams
    }

    return teams.filter((team) => team.departmentId === scopeDepartmentId)
  }, [teams, scopeDepartmentId])

  const teamOptionsForLearner = useMemo(() => {
    if (!teamForm.departmentId) {
      return teams
    }

    return teams.filter((team) => team.departmentId === teamForm.departmentId)
  }, [teamForm.departmentId, teams])

  const selectedTeamLearners = useMemo(
    () => learners.filter((learner) => learner.teamId === assignForm.teamId),
    [learners, assignForm.teamId],
  )

  const pagedCourses = useMemo(() => {
    const totalItems = coursesTotalCount
    const totalPages = Math.max(1, Math.ceil(totalItems / COURSE_PAGE_SIZE))
    return {
      items: courses,
      page: coursePage,
      pageSize: COURSE_PAGE_SIZE,
      totalItems,
      totalPages,
      startIndex: (coursePage - 1) * COURSE_PAGE_SIZE,
    }
  }, [courses, coursePage, coursesTotalCount])
  const pagedTeamsDirectory = useMemo(() => {
    const totalItems = teamsDirectoryTotalCount
    const totalPages = Math.max(1, Math.ceil(totalItems / TEAMS_DIRECTORY_PAGE_SIZE))
    return {
      items: teamsDirectoryRows,
      page: teamsDirectoryPage,
      pageSize: TEAMS_DIRECTORY_PAGE_SIZE,
      totalItems,
      totalPages,
      startIndex: (teamsDirectoryPage - 1) * TEAMS_DIRECTORY_PAGE_SIZE,
    }
  }, [teamsDirectoryRows, teamsDirectoryPage, teamsDirectoryTotalCount])
  const pagedManagerAssignments = useMemo(
    () => paginate(assignments, managerAssignmentPage, 10),
    [assignments, managerAssignmentPage],
  )
  const pagedMandatoryComplianceRows = useMemo(
    () => paginate(mandatoryComplianceRows, mandatoryGapPage, 10),
    [mandatoryComplianceRows, mandatoryGapPage],
  )

  const loadDashboardData = useCallback(async () => {
    try {
      const [deptData, teamData, learnerData, assignmentData, dashboardData, mandatoryComplianceData, allCoursesData] = await Promise.all([
        apiRequest('/departments'),
        apiRequest('/teams'),
        apiRequest('/learners'),
        apiRequest(`/assignments${scopeQuery}`),
        apiRequest(`/dashboard${scopeQuery}`),
        apiRequest(`/reports/mandatory-compliance${scopeQuery}`),
        apiRequest('/courses'),
      ])

      setDepartments(Array.isArray(deptData) ? deptData : [])
      setTeams(Array.isArray(teamData) ? teamData : [])
      setLearners(Array.isArray(learnerData) ? learnerData : [])
      setAssignments(Array.isArray(assignmentData) ? assignmentData : [])
      setMandatoryComplianceRows(Array.isArray(mandatoryComplianceData) ? mandatoryComplianceData : [])
      setDashboard(dashboardData ?? null)

      const normalizedAllCourses = Array.isArray(allCoursesData) ? allCoursesData : []
      setAllCourses(normalizedAllCourses)
      setAssignForm((prev) =>
        !prev.courseId && normalizedAllCourses.length > 0 ? { ...prev, courseId: normalizedAllCourses[0].id } : prev,
      )

      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.loadDashboard')))
    }
  }, [scopeQuery, t, setError])

  // Server-paginated: page/pageSize go to the API as query params, the pre-paging total comes
  // back via X-Total-Count (see apiRequestPage / PagingHelper.cs) rather than fetching every
  // matching course and slicing client-side.
  const searchCourses = useCallback(async (query, page = 1) => {
    setPendingAction('searchCourses')

    try {
      if (query) {
        // Syncing pulls from external providers and writes to the catalog, so it's a POST -
        // the GET below stays a safe, side-effect-free local read.
        await apiRequest(`/courses/sync?query=${encodeURIComponent(query)}`, { method: 'POST' })
      }

      const { items, totalCount } = await apiRequestPage(`/courses?query=${encodeURIComponent(query)}`, {
        page,
        pageSize: COURSE_PAGE_SIZE,
      })
      setCourses(items)
      setCoursesTotalCount(totalCount)
      setCoursePage(page)
    } catch (error) {
      setError(getErrorMessage(error, t('errors.loadCourses')))
    } finally {
      setPendingAction('')
    }
  }, [t, setError])

  const changeCoursePage = useCallback((page) => {
    void searchCourses(courseQuery, page)
  }, [searchCourses, courseQuery])

  // Server-paginated Teams Directory table - same apiRequestPage/X-Total-Count contract as the
  // course catalog above, independent of the full, unpaged `teams` list every dropdown uses.
  const loadTeamsDirectoryPage = useCallback(async (page) => {
    try {
      const { items, totalCount } = await apiRequestPage('/teams', { page, pageSize: TEAMS_DIRECTORY_PAGE_SIZE })
      setTeamsDirectoryRows(items)
      setTeamsDirectoryTotalCount(totalCount)
      setTeamsDirectoryPage(page)
    } catch (error) {
      setError(getErrorMessage(error, t('errors.loadTeamsDirectory')))
    }
  }, [t, setError])

  const searchSkillMatch = useCallback(async (normalizedSkills) => {
    if (!normalizedSkills) {
      setError(t('errors.enterSkillKeyword'))
      return
    }

    setSkillMatchLoading(true)

    try {
      const data = await apiRequest(`/reports/skill-match?${buildQueryString({
        skills: normalizedSkills,
        top: '30',
        departmentId: scopeDepartmentId || undefined,
        teamId: scopeTeamId || undefined,
      })}`)
      setSkillMatchRows(data)
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.fetchSkillMatch')))
    } finally {
      setSkillMatchLoading(false)
    }
  }, [scopeDepartmentId, scopeTeamId, t, setError])

  const createDepartment = useCallback(async (event) => {
    event.preventDefault()
    setPendingAction('createDepartment')

    try {
      await apiRequest('/departments', {
        method: 'POST',
        body: JSON.stringify({
          name: departmentForm.name.trim(),
          code: departmentForm.code.trim(),
        }),
      })

      setDepartmentForm(INITIAL_DEPARTMENT_FORM)
      setModalOpen(null)
      await loadDashboardData()
    } catch (error) {
      setError(getErrorMessage(error, t('errors.createDepartment')))
    } finally {
      setPendingAction('')
    }
  }, [departmentForm, loadDashboardData, t, setError])

  const createTeam = useCallback(async (event) => {
    event.preventDefault()
    setPendingAction('createTeam')

    try {
      await apiRequest('/teams', {
        method: 'POST',
        body: JSON.stringify({
          name: teamForm.name.trim(),
          departmentId: teamForm.departmentId,
          managerName: teamForm.managerName.trim(),
          managerEmail: teamForm.managerEmail.trim(),
        }),
      })

      setTeamForm(INITIAL_TEAM_FORM)
      setModalOpen(null)
      await loadDashboardData()
    } catch (error) {
      setError(getErrorMessage(error, t('errors.createTeam')))
    } finally {
      setPendingAction('')
    }
  }, [teamForm, loadDashboardData, t, setError])

  const createLearner = useCallback(async (event) => {
    event.preventDefault()
    setPendingAction('createLearner')

    try {
      await apiRequest('/learners', {
        method: 'POST',
        body: JSON.stringify({
          employeeCode: learnerForm.employeeCode.trim(),
          name: learnerForm.name.trim(),
          email: learnerForm.email.trim(),
          designation: learnerForm.designation.trim(),
          teamId: learnerForm.teamId || null,
        }),
      })

      setLearnerForm(INITIAL_LEARNER_FORM)
      setModalOpen(null)
      await loadDashboardData()
    } catch (error) {
      setError(getErrorMessage(error, t('errors.createLearner')))
    } finally {
      setPendingAction('')
    }
  }, [learnerForm, loadDashboardData, t, setError])

  const createAssignment = useCallback(async (event) => {
    event.preventDefault()

    const payload = {
      courseId: assignForm.courseId,
      learnerId: assignForm.targetType === 'individual' ? assignForm.learnerId : null,
      teamId: assignForm.targetType === 'team' ? assignForm.teamId : null,
      accessType: assignForm.accessType,
      dueDate: assignForm.accessType === 'Temporary' ? assignForm.dueDate || null : null,
    }

    setPendingAction('createAssignment')

    try {
      await apiRequest('/assignments', {
        method: 'POST',
        body: JSON.stringify(payload),
      })

      await loadDashboardData()
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.createAssignment')))
    } finally {
      setPendingAction('')
    }
  }, [assignForm, loadDashboardData, t, setError])

  const saveProgress = useCallback(async (assignmentId) => {
    const value = Number(progressEdits[assignmentId])
    if (Number.isNaN(value)) {
      return
    }

    setPendingAction(`saveProgress:${assignmentId}`)

    try {
      await apiRequest(`/assignments/${assignmentId}/progress`, {
        method: 'PATCH',
        body: JSON.stringify({ progressPercent: value }),
      })

      await loadDashboardData()
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.saveProgress')))
    } finally {
      setPendingAction('')
    }
  }, [progressEdits, loadDashboardData, t, setError])

  const syncUdemyProgress = useCallback(async () => {
    try {
      await apiRequest('/integrations/udemy/sync-progress', {
        method: 'POST',
      })

      await loadDashboardData()
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.syncUdemy')))
    }
  }, [loadDashboardData, t, setError])

  const syncLinkedInProgress = useCallback(async () => {
    try {
      await apiRequest('/integrations/linkedin/sync-progress', {
        method: 'POST',
      })

      await loadDashboardData()
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.syncLinkedIn')))
    }
  }, [loadDashboardData, t, setError])

  const toggleMandatory = useCallback(async (courseId, isMandatory) => {
    try {
      await apiRequest(`/courses/${courseId}/mandatory`, {
        method: 'PATCH',
        body: JSON.stringify({ isMandatory }),
      })

      await searchCourses(courseQuery, coursePage)
      await loadDashboardData()
    } catch (error) {
      setError(getErrorMessage(error, t('errors.updateMandatory')))
    }
  }, [searchCourses, courseQuery, coursePage, loadDashboardData, t, setError])

  // Manager override for a bad AI-extracted tag - mirrors the backend's
  // DELETE /api/courses/{id}/skill-tags (see CourseCatalogService.ClearSkillTagsAsync). Resets
  // the course to untagged; it's picked up for re-extraction on the next provider sync.
  const clearSkillTags = useCallback(async (courseId) => {
    setPendingAction(`clearSkillTags:${courseId}`)

    try {
      await apiRequest(`/courses/${courseId}/skill-tags`, { method: 'DELETE' })
      await searchCourses(courseQuery, coursePage)
    } catch (error) {
      setError(getErrorMessage(error, t('errors.clearSkillTags')))
    } finally {
      setPendingAction('')
    }
  }, [searchCourses, courseQuery, coursePage, t, setError])

  // Runs once on mount (when enabled). searchCourses/loadDashboardData both catch their own
  // errors internally and never reject, so there's nothing for an outer try/catch here to ever
  // observe - the two effects below (which do have real, exhaustive-deps-satisfying dependencies)
  // are what actually keep this data fresh after mount. `enabled` exists for a real (server-
  // authenticated) Learner session: every endpoint this hook calls is Manager-only, so firing
  // them for a Learner token would just be a guaranteed wall of 403s before LearnerSelfService
  // ever renders - see App.jsx.
  useEffect(() => {
    if (!enabled) {
      return
    }

    void searchCourses('')
  }, [searchCourses, enabled])

  useEffect(() => {
    if (!enabled) {
      return
    }

    void loadDashboardData()
  }, [loadDashboardData, enabled])

  useEffect(() => {
    if (!enabled) {
      return
    }

    void loadTeamsDirectoryPage(1)
  }, [loadTeamsDirectoryPage, enabled])

  useEffect(() => {
    setManagerAssignmentPage(1)
  }, [scopeQuery, assignments.length])

  useEffect(() => {
    setMandatoryGapPage(1)
  }, [scopeQuery, mandatoryComplianceRows.length])

  useEffect(() => {
    if (scopeTeamId && !filteredTeamsByScope.some((team) => team.id === scopeTeamId)) {
      setScopeTeamId('')
    }
  }, [scopeTeamId, filteredTeamsByScope])

  return {
    departments,
    teams,
    pagedTeamsDirectory,
    onTeamsDirectoryPageChange: loadTeamsDirectoryPage,
    learners,
    courses,
    allCourses,
    assignments,
    mandatoryComplianceRows,
    skillMatchRows,
    skillMatchLoading,
    dashboard,
    pendingAction,
    courseQuery,
    setCourseQuery,
    scopeDepartmentId,
    setScopeDepartmentId,
    scopeTeamId,
    setScopeTeamId,
    filteredTeamsByScope,
    modalOpen,
    setModalOpen,
    departmentForm,
    setDepartmentForm,
    teamForm,
    setTeamForm,
    learnerForm,
    setLearnerForm,
    assignForm,
    setAssignForm,
    progressEdits,
    setProgressEdits,
    teamOptionsForLearner,
    selectedTeamLearners,
    pagedCourses,
    coursePage,
    onCoursePageChange: changeCoursePage,
    pagedManagerAssignments,
    managerAssignmentPage,
    setManagerAssignmentPage,
    pagedMandatoryComplianceRows,
    mandatoryGapPage,
    setMandatoryGapPage,
    loadDashboardData,
    searchCourses,
    searchSkillMatch,
    createDepartment,
    createTeam,
    createLearner,
    createAssignment,
    saveProgress,
    syncUdemyProgress,
    syncLinkedInProgress,
    toggleMandatory,
    clearSkillTags,
  }
}
