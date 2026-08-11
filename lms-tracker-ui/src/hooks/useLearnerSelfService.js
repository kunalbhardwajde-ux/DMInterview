import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiRequest, apiRequestPage } from '../apiClient'
import { getErrorMessage } from '../utils/errorHandling'

const ASSIGNMENTS_PAGE_SIZE = 8

// Backs the real (server-authenticated) Learner session's self-service page - GET /learners/me
// and GET /assignments/mine, both scoped strictly to the id on the caller's own token (see
// LearnersModule.cs / AssignmentsModule.cs). Deliberately read-only: no progress-update call
// here, matching the explicit ask ("so that they can see the assigned courses").
export function useLearnerSelfService() {
  const { t } = useTranslation()

  const [profile, setProfile] = useState(null)
  const [assignments, setAssignments] = useState([])
  const [assignmentsTotalCount, setAssignmentsTotalCount] = useState(0)
  const [assignmentsPage, setAssignmentsPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadAssignmentsPage = useCallback(async (page) => {
    const { items, totalCount } = await apiRequestPage('/assignments/mine', { page, pageSize: ASSIGNMENTS_PAGE_SIZE })
    setAssignments(items)
    setAssignmentsTotalCount(totalCount)
    setAssignmentsPage(page)
  }, [])

  useEffect(() => {
    let isMounted = true

    const load = async () => {
      setLoading(true)
      try {
        const [profileData] = await Promise.all([apiRequest('/learners/me'), loadAssignmentsPage(1)])
        if (isMounted) {
          setProfile(profileData)
          setError('')
        }
      } catch (loadError) {
        if (isMounted) {
          setError(getErrorMessage(loadError, t('learnerSelfService.loadError')))
        }
      } finally {
        if (isMounted) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => {
      isMounted = false
    }
  }, [loadAssignmentsPage, t])

  const pagedAssignments = {
    items: assignments,
    page: assignmentsPage,
    pageSize: ASSIGNMENTS_PAGE_SIZE,
    totalItems: assignmentsTotalCount,
    totalPages: Math.max(1, Math.ceil(assignmentsTotalCount / ASSIGNMENTS_PAGE_SIZE)),
    startIndex: (assignmentsPage - 1) * ASSIGNMENTS_PAGE_SIZE,
  }

  return {
    profile,
    pagedAssignments,
    loading,
    error,
    onAssignmentsPageChange: loadAssignmentsPage,
  }
}
