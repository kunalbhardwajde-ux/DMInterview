import React from 'react'
import { useTranslation } from 'react-i18next'
import { PaginationControls } from '../../components/PaginationControls'
import { PagerSummary } from '../../components/PagerSummary'
import { SortableHeader } from '../../components/SortableHeader'

export function LearnerPortal({
  learnerPersona,
  setLearnerPersona,
  teamManagerTeamId,
  setTeamManagerTeamId,
  teams,
  teamMembers,
  teamManagerDashboard,
  teamManagerAssignments,
  pagedTeamManagerAssignments,
  teamManagerAssignmentPage,
  onTeamManagerAssignmentPageChange,
  teamManagerAssignmentSortKey,
  teamManagerAssignmentSortDirection,
  onTeamManagerAssignmentSort,
  teamManagerMandatoryGaps,
  pagedTeamManagerMandatoryGaps,
  teamManagerMandatoryGapsPage,
  onTeamManagerMandatoryGapsPageChange,
  teamManagerMandatoryGapsSortKey,
  teamManagerMandatoryGapsSortDirection,
  onTeamManagerMandatoryGapsSort,
  onRefreshTeamView,
  onRefreshUdemy,
  onRefreshLinkedIn,
  individualEmployeeCode,
  setIndividualEmployeeCode,
  individualLearner,
  onStartIndividualSession,
  onSignOut,
  learnerAssignments,
  pagedLearnerAssignments,
  learnerAssignmentPage,
  onLearnerAssignmentPageChange,
  learnerAssignmentSortKey,
  learnerAssignmentSortDirection,
  onLearnerAssignmentSort,
}) {
  const { t } = useTranslation()

  return (
    <>
      <section className="panel">
        <div className="panel-title-row">
          <h2>{t('learner.personas.title')}</h2>
          <p className="muted-inline">{t('learner.personas.description')}</p>
        </div>
        <div className="persona-switch">
          <button type="button" className={learnerPersona === 'individual' ? 'active' : ''} onClick={() => setLearnerPersona('individual')}>
            {t('learner.personas.individual')}
          </button>
          <button type="button" className={learnerPersona === 'teamManager' ? 'active' : ''} onClick={() => setLearnerPersona('teamManager')}>
            {t('learner.personas.teamManager')}
          </button>
        </div>
      </section>

      {learnerPersona === 'teamManager' ? (
        <>
          <section className="panel">
            <div className="panel-title-row">
              <h2>{t('learner.teamManager.myTeamProgress')}</h2>
            </div>
            <div className="form compact-grid">
              <select value={teamManagerTeamId} onChange={(event) => setTeamManagerTeamId(event.target.value)}>
                {teams.map((team) => (
                  <option key={team.id} value={team.id}>
                    {team.name} - {team.managerName}
                  </option>
                ))}
              </select>
              <button type="button" onClick={() => void onRefreshTeamView(teamManagerTeamId)}>
                {t('learner.teamManager.refreshTeamView')}
              </button>
            </div>
          </section>

          <section className="stats-grid">
            <article className="stat-card">
              <h3>{t('learner.teamManager.teamMembers')}</h3>
              <strong>{teamMembers.length}</strong>
            </article>
            <article className="stat-card">
              <h3>{t('learner.teamManager.assignments')}</h3>
              <strong>{teamManagerDashboard?.assignments ?? 0}</strong>
            </article>
            <article className="stat-card">
              <h3>{t('common.completed')}</h3>
              <strong>{teamManagerDashboard?.completed ?? 0}</strong>
            </article>
            <article className="stat-card">
              <h3>{t('learner.teamManager.inProgress')}</h3>
              <strong>{teamManagerDashboard?.inProgress ?? 0}</strong>
            </article>
            <article className="stat-card">
              <h3>{t('learner.teamManager.notStarted')}</h3>
              <strong>{teamManagerDashboard?.notStarted ?? 0}</strong>
            </article>
            <article className="stat-card">
              <h3>{t('common.completion')}</h3>
              <strong>{teamManagerDashboard?.completionRate ?? 0}%</strong>
            </article>
          </section>

          <section className="panel">
            <div className="panel-title-row">
              <h2>{t('learner.teamManager.teamCourseProgress')}</h2>
              <div className="action-row">
                <button type="button" onClick={() => void onRefreshUdemy()}>
                  {t('learner.teamManager.refreshUdemy')}
                </button>
                <button type="button" onClick={() => void onRefreshLinkedIn()}>
                  {t('learner.teamManager.refreshLinkedIn')}
                </button>
              </div>
            </div>
            {teamManagerAssignments.length === 0 ? (
              <p>{t('learner.teamManager.noAssignments')}</p>
            ) : (
              <>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <SortableHeader label={t('common.empCode')} sortKey="employeeCode" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.learner')} sortKey="learnerName" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.course')} sortKey="courseTitle" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.provider')} sortKey="provider" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.due')} sortKey="dueDate" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.progress')} sortKey="progressPercent" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                        <SortableHeader label={t('common.status')} sortKey="status" currentSortKey={teamManagerAssignmentSortKey} currentSortDirection={teamManagerAssignmentSortDirection} onSort={onTeamManagerAssignmentSort} />
                      </tr>
                    </thead>
                    <tbody>
                      {pagedTeamManagerAssignments.items.map((assignment) => (
                        <tr key={assignment.id}>
                          <td>{assignment.employeeCode}</td>
                          <td>{assignment.learnerName}</td>
                          <td>{assignment.courseTitle}</td>
                          <td>{assignment.provider}</td>
                          <td>{assignment.dueDate || t('common.na')}</td>
                          <td>{assignment.progressPercent}%</td>
                          <td>{t(`enums.status.${assignment.status}`, assignment.status)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <PagerSummary pagedResult={pagedTeamManagerAssignments} />
                <PaginationControls
                  page={teamManagerAssignmentPage}
                  totalPages={pagedTeamManagerAssignments.totalPages}
                  onPageChange={onTeamManagerAssignmentPageChange}
                />
              </>
            )}
          </section>

          <section className="panel">
            <div className="panel-title-row">
              <h2>{t('learner.teamManager.teamMandatoryGaps')}</h2>
              <p className="muted-inline">{t('learner.teamManager.gapsHint')}</p>
            </div>
            {teamManagerMandatoryGaps.length === 0 ? (
              <p>{t('learner.teamManager.allCompliant')}</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <SortableHeader label={t('common.empCode')} sortKey="employeeCode" currentSortKey={teamManagerMandatoryGapsSortKey} currentSortDirection={teamManagerMandatoryGapsSortDirection} onSort={onTeamManagerMandatoryGapsSort} />
                      <SortableHeader label={t('common.learner')} sortKey="learnerName" currentSortKey={teamManagerMandatoryGapsSortKey} currentSortDirection={teamManagerMandatoryGapsSortDirection} onSort={onTeamManagerMandatoryGapsSort} />
                      <SortableHeader label={t('common.pendingCount')} sortKey="pendingMandatoryCourses" currentSortKey={teamManagerMandatoryGapsSortKey} currentSortDirection={teamManagerMandatoryGapsSortDirection} onSort={onTeamManagerMandatoryGapsSort} />
                      <SortableHeader label={t('common.pendingCourses')} sortKey="pendingCourseTitles" currentSortKey={teamManagerMandatoryGapsSortKey} currentSortDirection={teamManagerMandatoryGapsSortDirection} onSort={onTeamManagerMandatoryGapsSort} />
                    </tr>
                  </thead>
                  <tbody>
                    {pagedTeamManagerMandatoryGaps.items.map((row) => (
                      <tr key={row.learnerId}>
                        <td>{row.employeeCode}</td>
                        <td>{row.learnerName}</td>
                        <td>{row.pendingMandatoryCourses}</td>
                        <td>{row.pendingCourseTitles}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {teamManagerMandatoryGaps.length > 0 ? (
              <>
                <PagerSummary pagedResult={pagedTeamManagerMandatoryGaps} />
                <PaginationControls
                  page={teamManagerMandatoryGapsPage}
                  totalPages={pagedTeamManagerMandatoryGaps.totalPages}
                  onPageChange={onTeamManagerMandatoryGapsPageChange}
                />
              </>
            ) : null}
          </section>
        </>
      ) : (
        <>
          {!individualLearner ? (
            <section className="panel">
              <h2>{t('learner.individual.loginTitle')}</h2>
              <p className="muted-inline">{t('learner.individual.loginHint')}</p>
              <form className="form learner-filter" onSubmit={onStartIndividualSession}>
                <input
                  value={individualEmployeeCode}
                  onChange={(event) => setIndividualEmployeeCode(event.target.value)}
                  placeholder={t('learner.individual.employeeCodePlaceholder')}
                  required
                />
                <button type="submit">{t('learner.individual.continue')}</button>
              </form>
            </section>
          ) : (
            <section className="panel">
              <div className="panel-title-row">
                <h2>{t('learner.individual.dashboardTitle')}</h2>
                <button type="button" className="ghost" onClick={onSignOut}>
                  {t('learner.individual.signOut')}
                </button>
              </div>
              <div className="identity-chip-row">
                <span className="identity-chip">{individualLearner.name}</span>
                <span className="identity-chip">{individualLearner.employeeCode}</span>
                <span className="identity-chip">{individualLearner.designation}</span>
              </div>
            </section>
          )}

          {individualLearner ? (
            <section className="panel">
              <div className="panel-title-row">
                <h2>{t('learner.individual.assignedCoursesTitle')}</h2>
                <div className="action-row">
                  <button type="button" onClick={() => void onRefreshUdemy()}>
                    {t('learner.individual.refreshUdemy')}
                  </button>
                  <button type="button" onClick={() => void onRefreshLinkedIn()}>
                    {t('learner.individual.refreshLinkedIn')}
                  </button>
                </div>
              </div>
              {learnerAssignments.length === 0 ? (
                <p>{t('learner.individual.noCourses')}</p>
              ) : (
                <>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <SortableHeader label={t('common.course')} sortKey="courseTitle" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <SortableHeader label={t('common.provider')} sortKey="provider" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <SortableHeader label={t('common.access')} sortKey="accessType" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <SortableHeader label={t('common.due')} sortKey="dueDate" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <SortableHeader label={t('common.progress')} sortKey="progressPercent" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <SortableHeader label={t('common.status')} sortKey="status" currentSortKey={learnerAssignmentSortKey} currentSortDirection={learnerAssignmentSortDirection} onSort={onLearnerAssignmentSort} />
                          <th>{t('common.launch')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pagedLearnerAssignments.items.map((assignment) => (
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
                  <PagerSummary pagedResult={pagedLearnerAssignments} />
                  <PaginationControls
                    page={learnerAssignmentPage}
                    totalPages={pagedLearnerAssignments.totalPages}
                    onPageChange={onLearnerAssignmentPageChange}
                  />
                </>
              )}
            </section>
          ) : null}
        </>
      )}
    </>
  )
}
