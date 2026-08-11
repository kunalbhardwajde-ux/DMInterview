import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiRequest } from '../apiClient'
import { paginate } from '../utils/pagination'
import { sortRows, useSortState } from '../utils/sorting'
import { getErrorMessage } from '../utils/errorHandling'

// Owns the Learner route's two personas: "individual" (an employee looking up their own
// assignments by employee code) and "teamManager" (browsing a team's assignments/compliance).
// Takes learners/teams/assignments from useManagerData rather than loading its own copy - that
// data is already fetched once at app mount regardless of route, so a second fetch here would
// just be a duplicate round trip for the same rows.
export function useLearnerPortalData({ learners, teams, assignments, setError }) {
  const { t } = useTranslation()

  const [learnerPersona, setLearnerPersona] = useState('individual')

  const [individualEmployeeCode, setIndividualEmployeeCode] = useState('')
  const [individualSessionLearnerId, setIndividualSessionLearnerId] = useState(
    () => localStorage.getItem('lms-individual-learner-id') ?? '',
  )
  const [learnerAssignmentPage, setLearnerAssignmentPage] = useState(1)

  const [teamManagerTeamId, setTeamManagerTeamId] = useState('')
  const [teamManagerAssignments, setTeamManagerAssignments] = useState([])
  const [teamManagerDashboard, setTeamManagerDashboard] = useState(null)
  const [teamManagerMandatoryGaps, setTeamManagerMandatoryGaps] = useState([])
  const [teamManagerAssignmentPage, setTeamManagerAssignmentPage] = useState(1)
  const [teamManagerMandatoryGapsPage, setTeamManagerMandatoryGapsPage] = useState(1)

  const learnerAssignmentSort = useSortState()
  const teamManagerAssignmentSort = useSortState()
  const teamManagerMandatoryGapsSort = useSortState()

  const learnerAssignments = useMemo(
    () => assignments.filter((assignment) => assignment.learnerId === individualSessionLearnerId),
    [assignments, individualSessionLearnerId],
  )

  const individualLearner = useMemo(
    () => learners.find((learner) => learner.id === individualSessionLearnerId) ?? null,
    [learners, individualSessionLearnerId],
  )

  const teamMembers = useMemo(
    () => learners.filter((learner) => learner.teamId === teamManagerTeamId),
    [learners, teamManagerTeamId],
  )

  const sortedLearnerAssignments = useMemo(
    () => sortRows(learnerAssignments, learnerAssignmentSort.sortKey, learnerAssignmentSort.sortDirection),
    [learnerAssignments, learnerAssignmentSort.sortKey, learnerAssignmentSort.sortDirection],
  )
  const pagedLearnerAssignments = useMemo(
    () => paginate(sortedLearnerAssignments, learnerAssignmentPage, 8),
    [sortedLearnerAssignments, learnerAssignmentPage],
  )
  const sortedTeamManagerAssignments = useMemo(
    () => sortRows(teamManagerAssignments, teamManagerAssignmentSort.sortKey, teamManagerAssignmentSort.sortDirection),
    [teamManagerAssignments, teamManagerAssignmentSort.sortKey, teamManagerAssignmentSort.sortDirection],
  )
  const pagedTeamManagerAssignments = useMemo(
    () => paginate(sortedTeamManagerAssignments, teamManagerAssignmentPage, 10),
    [sortedTeamManagerAssignments, teamManagerAssignmentPage],
  )
  const sortedTeamManagerMandatoryGaps = useMemo(
    () => sortRows(teamManagerMandatoryGaps, teamManagerMandatoryGapsSort.sortKey, teamManagerMandatoryGapsSort.sortDirection),
    [teamManagerMandatoryGaps, teamManagerMandatoryGapsSort.sortKey, teamManagerMandatoryGapsSort.sortDirection],
  )
  const pagedTeamManagerMandatoryGaps = useMemo(
    () => paginate(sortedTeamManagerMandatoryGaps, teamManagerMandatoryGapsPage, 10),
    [sortedTeamManagerMandatoryGaps, teamManagerMandatoryGapsPage],
  )
  const requestLearnerAssignmentSort = useCallback((key) => {
    learnerAssignmentSort.requestSort(key)
    setLearnerAssignmentPage(1)
  }, [learnerAssignmentSort])
  const requestTeamManagerAssignmentSort = useCallback((key) => {
    teamManagerAssignmentSort.requestSort(key)
    setTeamManagerAssignmentPage(1)
  }, [teamManagerAssignmentSort])
  const requestTeamManagerMandatoryGapsSort = useCallback((key) => {
    teamManagerMandatoryGapsSort.requestSort(key)
    setTeamManagerMandatoryGapsPage(1)
  }, [teamManagerMandatoryGapsSort])

  const loadTeamPersonaData = useCallback(async (teamId) => {
    try {
      const [assignmentData, dashboardData, mandatoryData] = await Promise.all([
        apiRequest(`/assignments?teamId=${teamId}`),
        apiRequest(`/dashboard?teamId=${teamId}`),
        apiRequest(`/reports/mandatory-compliance?teamId=${teamId}`),
      ])

      setTeamManagerAssignments(assignmentData)
      setTeamManagerDashboard(dashboardData)
      setTeamManagerMandatoryGaps(mandatoryData)
      setError('')
    } catch (error) {
      setError(getErrorMessage(error, t('errors.loadTeamView')))
    }
  }, [t, setError])

  function startIndividualSession(event) {
    event.preventDefault()

    const normalizedCode = individualEmployeeCode.trim().toUpperCase()
    if (!normalizedCode) {
      setError(t('errors.enterEmployeeCode'))
      return
    }

    const learner = learners.find((item) => item.employeeCode.toUpperCase() === normalizedCode)
    if (!learner) {
      setError(t('errors.employeeCodeNotFound'))
      return
    }

    setIndividualSessionLearnerId(learner.id)
    localStorage.setItem('lms-individual-learner-id', learner.id)
    setError('')
  }

  function signOutIndividualSession() {
    setIndividualSessionLearnerId('')
    setIndividualEmployeeCode('')
    localStorage.removeItem('lms-individual-learner-id')
  }

  useEffect(() => {
    if (!teamManagerTeamId && teams.length > 0) {
      setTeamManagerTeamId(teams[0].id)
    }
  }, [teams, teamManagerTeamId])

  useEffect(() => {
    setLearnerAssignmentPage(1)
  }, [individualSessionLearnerId, learnerAssignments.length])

  useEffect(() => {
    if (!individualSessionLearnerId) {
      return
    }

    const exists = learners.some((learner) => learner.id === individualSessionLearnerId)
    if (!exists) {
      setIndividualSessionLearnerId('')
      localStorage.removeItem('lms-individual-learner-id')
    }
  }, [learners, individualSessionLearnerId])

  useEffect(() => {
    setTeamManagerAssignmentPage(1)
  }, [teamManagerTeamId, teamManagerAssignments.length])

  useEffect(() => {
    setTeamManagerMandatoryGapsPage(1)
  }, [teamManagerTeamId, teamManagerMandatoryGaps.length])

  useEffect(() => {
    if (learnerPersona !== 'teamManager' || !teamManagerTeamId) {
      return
    }

    void loadTeamPersonaData(teamManagerTeamId)
  }, [learnerPersona, teamManagerTeamId, loadTeamPersonaData])

  return {
    learnerPersona,
    setLearnerPersona,
    teamManagerTeamId,
    setTeamManagerTeamId,
    teamMembers,
    teamManagerDashboard,
    teamManagerAssignments,
    pagedTeamManagerAssignments,
    teamManagerAssignmentPage,
    setTeamManagerAssignmentPage,
    teamManagerAssignmentSortKey: teamManagerAssignmentSort.sortKey,
    teamManagerAssignmentSortDirection: teamManagerAssignmentSort.sortDirection,
    requestTeamManagerAssignmentSort,
    teamManagerMandatoryGaps,
    pagedTeamManagerMandatoryGaps,
    teamManagerMandatoryGapsPage,
    setTeamManagerMandatoryGapsPage,
    teamManagerMandatoryGapsSortKey: teamManagerMandatoryGapsSort.sortKey,
    teamManagerMandatoryGapsSortDirection: teamManagerMandatoryGapsSort.sortDirection,
    requestTeamManagerMandatoryGapsSort,
    loadTeamPersonaData,
    individualEmployeeCode,
    setIndividualEmployeeCode,
    individualLearner,
    startIndividualSession,
    signOutIndividualSession,
    learnerAssignments,
    pagedLearnerAssignments,
    learnerAssignmentPage,
    setLearnerAssignmentPage,
    learnerAssignmentSortKey: learnerAssignmentSort.sortKey,
    learnerAssignmentSortDirection: learnerAssignmentSort.sortDirection,
    requestLearnerAssignmentSort,
  }
}
