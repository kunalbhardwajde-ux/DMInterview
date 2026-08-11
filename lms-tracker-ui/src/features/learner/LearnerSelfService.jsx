import React from 'react'
import { useTranslation } from 'react-i18next'
import { useLearnerSelfService } from '../../hooks/useLearnerSelfService'
import { PaginationControls } from '../../components/PaginationControls'
import { PagerSummary } from '../../components/PagerSummary'
import { SortableHeader } from '../../components/SortableHeader'

// The real, server-authenticated Learner experience - distinct from LearnerPortal, which is the
// Manager-facing simulation of "what would a learner/team-manager see" (still useful as a demo,
// but it trusts whatever employee code was typed into it; this page trusts only the JWT).
export function LearnerSelfService({ onSignOut }) {
  const { t } = useTranslation()
  const {
    profile,
    pagedAssignments,
    loading,
    error,
    onAssignmentsPageChange,
    assignmentsSortKey,
    assignmentsSortDirection,
    onAssignmentsSort,
  } = useLearnerSelfService()

  return (
    <main className="page learner-page">
      {error ? <div className="error-box">{error}</div> : null}

      <section className="panel">
        <div className="panel-title-row">
          <h2>{t('learnerSelfService.dashboardTitle')}</h2>
          <button type="button" className="ghost" onClick={onSignOut}>
            {t('auth.signOut')}
          </button>
        </div>
        {profile ? (
          <div className="identity-chip-row">
            <span className="identity-chip">{profile.name}</span>
            <span className="identity-chip">{profile.employeeCode}</span>
            <span className="identity-chip">{profile.designation}</span>
            {profile.teamName ? <span className="identity-chip">{profile.teamName}</span> : null}
          </div>
        ) : null}
      </section>

      <section className="panel">
        <div className="panel-title-row">
          <h2>{t('learnerSelfService.assignedCoursesTitle')}</h2>
        </div>
        {loading ? (
          <p>{t('common.loading')}</p>
        ) : pagedAssignments.items.length === 0 && pagedAssignments.totalItems === 0 ? (
          <p>{t('learnerSelfService.noCourses')}</p>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <SortableHeader label={t('common.course')} sortKey="courseTitle" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <SortableHeader label={t('common.provider')} sortKey="provider" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <SortableHeader label={t('common.access')} sortKey="accessType" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <SortableHeader label={t('common.due')} sortKey="dueDate" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <SortableHeader label={t('common.progress')} sortKey="progressPercent" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <SortableHeader label={t('common.status')} sortKey="status" currentSortKey={assignmentsSortKey} currentSortDirection={assignmentsSortDirection} onSort={onAssignmentsSort} />
                    <th>{t('common.launch')}</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedAssignments.items.map((assignment) => (
                    <tr key={assignment.id}>
                      <td>{assignment.courseTitle}</td>
                      <td>{assignment.provider}</td>
                      <td>{t(`enums.accessType.${assignment.accessType}`, assignment.accessType)}</td>
                      <td>{assignment.dueDate || t('common.na')}</td>
                      <td>{assignment.progressPercent}%</td>
                      <td>{t(`enums.status.${assignment.status}`, assignment.status)}</td>
                      <td>
                        <a href={assignment.launchUrl} target="_blank" rel="noreferrer">
                          {t('common.open')}
                        </a>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <PagerSummary pagedResult={pagedAssignments} />
            <PaginationControls
              page={pagedAssignments.page}
              totalPages={pagedAssignments.totalPages}
              onPageChange={onAssignmentsPageChange}
            />
          </>
        )}
      </section>
    </main>
  )
}
