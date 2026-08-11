import React from 'react'
import { useTranslation } from 'react-i18next'
import { ManagerDashboard } from './ManagerDashboard'
import { ManagerSection } from './ManagerSection'
import { SkillMatchPanel } from '../skillMatch/SkillMatchPanel'
import { PaginationControls } from '../../components/PaginationControls'
import { PagerSummary } from '../../components/PagerSummary'
import { Modal } from '../../components/Modal'

export function ManagerPage({
  departments,
  teams,
  pagedTeamsDirectory,
  onTeamsDirectoryPageChange,
  learners,
  allCourses,
  dashboard,
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
  teamOptionsForLearner,
  assignForm,
  setAssignForm,
  selectedTeamLearners,
  pendingAction,
  courseQuery,
  setCourseQuery,
  pagedCourses,
  onCoursePageChange,
  onSearchCourses,
  onToggleMandatory,
  onClearSkillTags,
  onCreateDepartment,
  onCreateTeam,
  onCreateLearner,
  onCreateAssignment,
  pagedManagerAssignments,
  setManagerAssignmentPage,
  progressEdits,
  setProgressEdits,
  onSaveProgress,
  onSyncUdemy,
  onSyncLinkedIn,
  mandatoryComplianceRows,
  pagedMandatoryComplianceRows,
  setMandatoryGapPage,
  skillMatchRows,
  skillMatchLoading,
  onAnalyzeSkillMatch,
}) {
  const { t } = useTranslation()

  return (
    <main className="page manager-page">
      <ManagerSection
        title={t('manager.scope.title')}
        actions={(
          <>
            <button type="button" onClick={() => setModalOpen('department')}>
              {t('manager.scope.newDepartment')}
            </button>
            <button type="button" onClick={() => setModalOpen('team')}>
              {t('manager.scope.newTeam')}
            </button>
            <button type="button" onClick={() => setModalOpen('learner')}>
              {t('manager.scope.newEmployee')}
            </button>
          </>
        )}
      >
        <div className="form compact-grid">
          <select value={scopeDepartmentId} onChange={(event) => setScopeDepartmentId(event.target.value)}>
            <option value="">{t('manager.scope.allDepartments')}</option>
            {departments.map((department) => (
              <option key={department.id} value={department.id}>
                {department.name} ({department.code})
              </option>
            ))}
          </select>
          <select value={scopeTeamId} onChange={(event) => setScopeTeamId(event.target.value)}>
            <option value="">{t('manager.scope.allTeams')}</option>
            {filteredTeamsByScope.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
          <button
            type="button"
            onClick={() => {
              setScopeDepartmentId('')
              setScopeTeamId('')
            }}
          >
            {t('manager.scope.resetFilters')}
          </button>
        </div>
      </ManagerSection>

      <ManagerDashboard dashboard={dashboard} />

      <ManagerSection title={t('manager.courseCatalog.title')} description={t('manager.courseCatalog.description')}>
        <div className="search-row">
          <input
            value={courseQuery}
            onChange={(event) => setCourseQuery(event.target.value)}
            placeholder={t('manager.courseCatalog.searchPlaceholder')}
          />
          <button type="button" disabled={pendingAction === 'searchCourses'} onClick={() => void onSearchCourses(courseQuery)}>
            {pendingAction === 'searchCourses' ? t('common.searching') : t('common.search')}
          </button>
        </div>
        <ul className="course-list">
          {pagedCourses.items.map((course) => (
            <li key={course.id} className="course-list-item">
              <div className="course-list-row">
                <span className="course-line">
                  {course.title} <em>({course.provider})</em>
                  {course.isMandatory ? <strong className="mandatory-pill">{t('manager.courseCatalog.mandatory')}</strong> : null}
                </span>
                <button
                  type="button"
                  className="ghost"
                  onClick={() => void onToggleMandatory(course.id, !course.isMandatory)}
                >
                  {course.isMandatory ? t('manager.courseCatalog.removeMandatory') : t('manager.courseCatalog.markMandatory')}
                </button>
              </div>
              {course.skillTags && course.skillTags.length > 0 ? (
                <div className="skill-tags-row">
                  <span className="skill-tags-badge" title={t('manager.courseCatalog.aiTagsLabel')}>
                    {t('manager.courseCatalog.aiTagsLabel')}
                  </span>
                  <ul className="skill-tags-list">
                    {course.skillTags.map((tag) => (
                      <li key={tag} className="skill-tag-chip">{tag}</li>
                    ))}
                  </ul>
                  <button
                    type="button"
                    className="ghost skill-tags-clear"
                    disabled={pendingAction === `clearSkillTags:${course.id}`}
                    onClick={() => void onClearSkillTags(course.id)}
                  >
                    {pendingAction === `clearSkillTags:${course.id}`
                      ? t('manager.courseCatalog.clearingTags')
                      : t('manager.courseCatalog.clearTags')}
                  </button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
        <PagerSummary pagedResult={pagedCourses} />
        <PaginationControls
          page={pagedCourses.page}
          totalPages={pagedCourses.totalPages}
          onPageChange={onCoursePageChange}
        />
      </ManagerSection>

      <ManagerSection title={t('manager.assignForm.title')} description={t('manager.assignForm.description')}>
        <form onSubmit={onCreateAssignment} className="form compact-grid">
          <select
            value={assignForm.courseId}
            onChange={(event) => setAssignForm((prev) => ({ ...prev, courseId: event.target.value }))}
            required
          >
            <option value="">{t('manager.assignForm.selectCourse')}</option>
            {allCourses.map((course) => (
              <option key={course.id} value={course.id}>
                {course.title}
              </option>
            ))}
          </select>

          <select
            value={assignForm.targetType}
            onChange={(event) => setAssignForm((prev) => ({ ...prev, targetType: event.target.value }))}
          >
            <option value="individual">{t('manager.assignForm.individual')}</option>
            <option value="team">{t('manager.assignForm.team')}</option>
          </select>

          {assignForm.targetType === 'individual' ? (
            <select
              value={assignForm.learnerId}
              onChange={(event) => setAssignForm((prev) => ({ ...prev, learnerId: event.target.value }))}
              required
            >
              <option value="">{t('manager.assignForm.selectLearner')}</option>
              {learners.map((learner) => (
                <option key={learner.id} value={learner.id}>
                  {learner.name} ({learner.employeeCode})
                </option>
              ))}
            </select>
          ) : (
            <select
              value={assignForm.teamId}
              onChange={(event) => setAssignForm((prev) => ({ ...prev, teamId: event.target.value }))}
              required
            >
              <option value="">{t('manager.assignForm.selectTeam')}</option>
              {teams.map((team) => (
                <option key={team.id} value={team.id}>
                  {team.name}
                </option>
              ))}
            </select>
          )}

          <select
            value={assignForm.accessType}
            onChange={(event) => setAssignForm((prev) => ({ ...prev, accessType: event.target.value }))}
          >
            <option value="Temporary">{t('manager.assignForm.temporary')}</option>
            <option value="Permanent">{t('manager.assignForm.permanent')}</option>
          </select>

          <input
            type="date"
            disabled={assignForm.accessType !== 'Temporary'}
            value={assignForm.dueDate}
            onChange={(event) => setAssignForm((prev) => ({ ...prev, dueDate: event.target.value }))}
          />

          <button type="submit" disabled={pendingAction === 'createAssignment'}>
            {pendingAction === 'createAssignment' ? t('manager.assignForm.assigning') : t('manager.assignForm.assign')}
          </button>
        </form>
        {assignForm.targetType === 'team' && assignForm.teamId ? (
          <p className="hint">{t('manager.assignForm.teamHint', { count: selectedTeamLearners.length })}</p>
        ) : null}
      </ManagerSection>

      <ManagerSection title={t('manager.progressTracker.title')} description={t('manager.progressTracker.description')}>
        <div className="action-row">
          <button type="button" onClick={() => void onSyncUdemy()}>
            {t('manager.progressTracker.syncUdemy')}
          </button>
          <button type="button" onClick={() => void onSyncLinkedIn()}>
            {t('manager.progressTracker.syncLinkedIn')}
          </button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>{t('common.empCode')}</th>
                <th>{t('common.learner')}</th>
                <th>{t('common.department')}</th>
                <th>{t('common.team')}</th>
                <th>{t('common.course')}</th>
                <th>{t('common.access')}</th>
                <th>{t('common.due')}</th>
                <th>{t('common.launch')}</th>
                <th>{t('common.progress')}</th>
                <th>{t('common.status')}</th>
              </tr>
            </thead>
            <tbody>
              {pagedManagerAssignments.items.map((assignment) => (
                <tr key={assignment.id}>
                  <td>{assignment.employeeCode}</td>
                  <td>{assignment.learnerName}</td>
                  <td>{assignment.departmentName || t('common.na')}</td>
                  <td>{assignment.teamName || t('common.na')}</td>
                  <td>{assignment.courseTitle}</td>
                  <td>{t(`enums.accessType.${assignment.accessType}`, assignment.accessType)}</td>
                  <td>{assignment.dueDate || t('common.na')}</td>
                  <td>
                    <a href={assignment.launchUrl} target="_blank" rel="noreferrer">
                      {t('common.open')}
                    </a>
                  </td>
                  <td>
                    <div className="progress-input">
                      <input
                        type="number"
                        min="0"
                        max="100"
                        value={
                          progressEdits[assignment.id] !== undefined
                            ? progressEdits[assignment.id]
                            : assignment.progressPercent
                        }
                        onChange={(event) =>
                          setProgressEdits((prev) => ({ ...prev, [assignment.id]: event.target.value }))
                        }
                      />
                      <button
                        type="button"
                        disabled={pendingAction === `saveProgress:${assignment.id}`}
                        onClick={() => void onSaveProgress(assignment.id)}
                      >
                        {pendingAction === `saveProgress:${assignment.id}` ? t('common.saving') : t('common.save')}
                      </button>
                    </div>
                  </td>
                  <td>{t(`enums.status.${assignment.status}`, assignment.status)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PagerSummary pagedResult={pagedManagerAssignments} />
        <PaginationControls
          page={pagedManagerAssignments.page}
          totalPages={pagedManagerAssignments.totalPages}
          onPageChange={setManagerAssignmentPage}
        />
      </ManagerSection>

      <ManagerSection title={t('manager.mandatoryGaps.title')} description={t('manager.mandatoryGaps.description')}>
        <p className="muted-inline">{t('manager.mandatoryGaps.hint')}</p>
        {mandatoryComplianceRows.length === 0 ? (
          <p>{t('manager.mandatoryGaps.allCompliant')}</p>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>{t('common.empCode')}</th>
                    <th>{t('common.employee')}</th>
                    <th>{t('common.department')}</th>
                    <th>{t('common.team')}</th>
                    <th>{t('common.pendingCount')}</th>
                    <th>{t('common.pendingCourses')}</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedMandatoryComplianceRows.items.map((row) => (
                    <tr key={row.learnerId}>
                      <td>{row.employeeCode}</td>
                      <td>{row.learnerName}</td>
                      <td>{row.departmentName || t('common.na')}</td>
                      <td>{row.teamName || t('common.na')}</td>
                      <td>{row.pendingMandatoryCourses}</td>
                      <td>{row.pendingCourseTitles}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <PagerSummary pagedResult={pagedMandatoryComplianceRows} />
            <PaginationControls
              page={pagedMandatoryComplianceRows.page}
              totalPages={pagedMandatoryComplianceRows.totalPages}
              onPageChange={setMandatoryGapPage}
            />
          </>
        )}
      </ManagerSection>

      <ManagerSection title={t('manager.teamsDirectory.title')} description={t('manager.teamsDirectory.description')}>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>{t('common.name')}</th>
                <th>{t('common.department')}</th>
                <th>{t('common.managerName')}</th>
                <th>{t('common.managerEmail')}</th>
              </tr>
            </thead>
            <tbody>
              {pagedTeamsDirectory.items.map((team) => (
                <tr key={team.id}>
                  <td>{team.name}</td>
                  <td>{team.departmentName || t('common.na')}</td>
                  <td>{team.managerName}</td>
                  <td>{team.managerEmail}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PagerSummary pagedResult={pagedTeamsDirectory} />
        <PaginationControls
          page={pagedTeamsDirectory.page}
          totalPages={pagedTeamsDirectory.totalPages}
          onPageChange={onTeamsDirectoryPageChange}
        />
      </ManagerSection>

      <SkillMatchPanel rows={skillMatchRows} loading={skillMatchLoading} onAnalyze={(skills) => void onAnalyzeSkillMatch(skills)} />

      {modalOpen ? <div className="modal-backdrop" onClick={() => setModalOpen(null)} /> : null}

      {modalOpen === 'department' ? (
        <Modal title={t('modals.department.title')} onClose={() => setModalOpen(null)}>
          <form className="form" onSubmit={onCreateDepartment}>
            <input
              value={departmentForm.name}
              onChange={(event) => setDepartmentForm((prev) => ({ ...prev, name: event.target.value }))}
              placeholder={t('modals.department.namePlaceholder')}
              aria-label={t('modals.department.namePlaceholder')}
              required
            />
            <input
              value={departmentForm.code}
              onChange={(event) => setDepartmentForm((prev) => ({ ...prev, code: event.target.value }))}
              placeholder={t('modals.department.codePlaceholder')}
              aria-label={t('modals.department.codePlaceholder')}
              required
            />
            <div className="modal-actions">
              <button type="button" className="ghost" onClick={() => setModalOpen(null)}>
                {t('common.cancel')}
              </button>
              <button type="submit" disabled={pendingAction === 'createDepartment'}>
                {pendingAction === 'createDepartment' ? t('common.saving') : t('modals.department.save')}
              </button>
            </div>
          </form>
        </Modal>
      ) : null}

      {modalOpen === 'team' ? (
        <Modal title={t('modals.team.title')} onClose={() => setModalOpen(null)}>
          <form className="form" onSubmit={onCreateTeam}>
            <input
              value={teamForm.name}
              onChange={(event) => setTeamForm((prev) => ({ ...prev, name: event.target.value }))}
              placeholder={t('modals.team.namePlaceholder')}
              aria-label={t('modals.team.namePlaceholder')}
              required
            />
            <select
              value={teamForm.departmentId}
              onChange={(event) => setTeamForm((prev) => ({ ...prev, departmentId: event.target.value }))}
              aria-label={t('modals.team.selectDepartment')}
              required
            >
              <option value="">{t('modals.team.selectDepartment')}</option>
              {departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </select>
            <input
              value={teamForm.managerName}
              onChange={(event) => setTeamForm((prev) => ({ ...prev, managerName: event.target.value }))}
              placeholder={t('modals.team.managerNamePlaceholder')}
              aria-label={t('modals.team.managerNamePlaceholder')}
              required
            />
            <input
              type="email"
              value={teamForm.managerEmail}
              onChange={(event) => setTeamForm((prev) => ({ ...prev, managerEmail: event.target.value }))}
              placeholder={t('modals.team.managerEmailPlaceholder')}
              aria-label={t('modals.team.managerEmailPlaceholder')}
              required
            />
            <div className="modal-actions">
              <button type="button" className="ghost" onClick={() => setModalOpen(null)}>
                {t('common.cancel')}
              </button>
              <button type="submit" disabled={pendingAction === 'createTeam'}>
                {pendingAction === 'createTeam' ? t('common.saving') : t('modals.team.save')}
              </button>
            </div>
          </form>
        </Modal>
      ) : null}

      {modalOpen === 'learner' ? (
        <Modal title={t('modals.learner.title')} onClose={() => setModalOpen(null)}>
          <form className="form" onSubmit={onCreateLearner}>
            <input
              value={learnerForm.employeeCode}
              onChange={(event) => setLearnerForm((prev) => ({ ...prev, employeeCode: event.target.value }))}
              placeholder={t('modals.learner.employeeCodePlaceholder')}
              aria-label={t('modals.learner.employeeCodePlaceholder')}
              required
            />
            <input
              value={learnerForm.name}
              onChange={(event) => setLearnerForm((prev) => ({ ...prev, name: event.target.value }))}
              placeholder={t('modals.learner.namePlaceholder')}
              aria-label={t('modals.learner.namePlaceholder')}
              required
            />
            <input
              type="email"
              value={learnerForm.email}
              onChange={(event) => setLearnerForm((prev) => ({ ...prev, email: event.target.value }))}
              placeholder={t('modals.learner.emailPlaceholder')}
              aria-label={t('modals.learner.emailPlaceholder')}
              required
            />
            <input
              value={learnerForm.designation}
              onChange={(event) => setLearnerForm((prev) => ({ ...prev, designation: event.target.value }))}
              placeholder={t('modals.learner.designationPlaceholder')}
              aria-label={t('modals.learner.designationPlaceholder')}
              required
            />
            <select
              value={learnerForm.teamId}
              onChange={(event) => setLearnerForm((prev) => ({ ...prev, teamId: event.target.value }))}
              aria-label={t('modals.learner.noTeam')}
            >
              <option value="">{t('modals.learner.noTeam')}</option>
              {teamOptionsForLearner.map((team) => (
                <option key={team.id} value={team.id}>
                  {team.name}
                </option>
              ))}
            </select>
            <div className="modal-actions">
              <button type="button" className="ghost" onClick={() => setModalOpen(null)}>
                {t('common.cancel')}
              </button>
              <button type="submit" disabled={pendingAction === 'createLearner'}>
                {pendingAction === 'createLearner' ? t('common.saving') : t('modals.learner.save')}
              </button>
            </div>
          </form>
        </Modal>
      ) : null}
    </main>
  )
}
