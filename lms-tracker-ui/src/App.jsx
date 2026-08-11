import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'
import './App.css'
import { apiConfig } from './apiClient'
import { useTheme, themes } from './hooks/useTheme'
import { useRoute } from './hooks/useRoute'
import { useAuthSession } from './hooks/useAuthSession'
import { useManagerData } from './hooks/useManagerData'
import { useLearnerPortalData } from './hooks/useLearnerPortalData'
import { ManagerPage } from './features/manager/ManagerPage'
import { LearnerPortal } from './features/learner/LearnerPortal'
import { LearnerSelfService } from './features/learner/LearnerSelfService'
import { LoginGate } from './components/LoginGate'

function App() {
  const { t } = useTranslation()
  const { route, goTo } = useRoute()
  const {
    theme,
    themePreview,
    setThemePreview,
    themeMenuOpen,
    setThemeMenuOpen,
    applyTheme,
  } = useTheme()
  const { authToken, authRole, sessionExpiringSoon, warningWindowMinutes, handleLogin, handleSignOut } = useAuthSession()
  const [error, setError] = useState('')

  // A real Learner session never needs Manager data - LearnerSelfService below is a completely
  // separate page that only calls /learners/me and /assignments/mine (see useManagerData's
  // `enabled` doc comment for why this matters, not just why it's tidy).
  const isRealLearnerSession = !apiConfig.useMockApi && authRole === 'Learner'
  const managerData = useManagerData({ setError, enabled: !isRealLearnerSession })
  const learnerPortalData = useLearnerPortalData({
    learners: managerData.learners,
    teams: managerData.teams,
    assignments: managerData.assignments,
    setError,
  })

  if (!apiConfig.useMockApi && !authToken) {
    return <LoginGate onLogin={handleLogin} />
  }

  if (isRealLearnerSession) {
    return <LearnerSelfService onSignOut={handleSignOut} />
  }

  return (
    <div className="page-shell">
      <header className="topbar">
        <div className="brand">
          <h1>{t('app.title')}</h1>
          <p>{t('app.tagline')}</p>
          <p className="muted-inline">
            {t('app.apiMode', { mode: apiConfig.useMockApi ? t('app.apiModeMock') : t('app.apiModeReal') })}
          </p>
        </div>

        <div className="topbar-actions">
          {apiConfig.useMockApi ? null : (
            <button type="button" className="ghost-button" onClick={handleSignOut}>
              {t('auth.signOut')}
            </button>
          )}
          <nav className="route-switch">
            <button
              type="button"
              className={route === '/manager' ? 'active' : ''}
              onClick={() => goTo('/manager')}
            >
              {t('nav.manager')}
            </button>
            <button
              type="button"
              className={route === '/learner' ? 'active' : ''}
              onClick={() => goTo('/learner')}
            >
              {t('nav.learner')}
            </button>
          </nav>

          <div className="theme-menu-wrap">
            <button type="button" className="menu-button" onClick={() => setThemeMenuOpen((open) => !open)}>
              {t('nav.menu')}
            </button>
            {themeMenuOpen ? (
              <div className="theme-menu" onMouseLeave={() => setThemePreview('')}>
                <p>{t('theme.heading')}</p>
                <p className="theme-menu-note">{t('theme.hint')}</p>
                {themes.map((option) => (
                  <button
                    key={option.id}
                    type="button"
                    className={`theme-option ${theme === option.id ? 'selected' : ''}`}
                    onMouseEnter={() => setThemePreview(option.id)}
                    onFocus={() => setThemePreview(option.id)}
                    onBlur={() => setThemePreview('')}
                    onClick={() => applyTheme(option.id)}
                  >
                    <span className="theme-option-title">{t(`theme.names.${option.id}`)}</span>
                    <span className="theme-option-swatches" aria-hidden="true">
                      <span style={{ backgroundColor: option.preview.bg }} />
                      <span style={{ backgroundColor: option.preview.panel }} />
                      <span style={{ backgroundColor: option.preview.brand }} />
                    </span>
                    <span className="theme-option-sample">{t('theme.sampleLabel')}</span>
                  </button>
                ))}
                {themePreview ? (
                  <p className="theme-menu-note">
                    {t('theme.previewing', { name: t(`theme.names.${themePreview}`) })}
                  </p>
                ) : null}
              </div>
            ) : null}
          </div>
        </div>
      </header>

      {sessionExpiringSoon ? (
        <div className="session-expiry-banner" role="alert">
          <span>{t('auth.sessionExpiringSoon', { minutes: warningWindowMinutes })}</span>
          <button type="button" className="ghost" onClick={handleSignOut}>
            {t('auth.signInAgain')}
          </button>
        </div>
      ) : null}

      {error ? <div className="error-box">{error}</div> : null}

      {route === '/manager' ? (
        <ManagerPage
          departments={managerData.departments}
          teams={managerData.teams}
          pagedTeamsDirectory={managerData.pagedTeamsDirectory}
          onTeamsDirectoryPageChange={managerData.onTeamsDirectoryPageChange}
          learners={managerData.learners}
          allCourses={managerData.allCourses}
          dashboard={managerData.dashboard}
          scopeDepartmentId={managerData.scopeDepartmentId}
          setScopeDepartmentId={managerData.setScopeDepartmentId}
          scopeTeamId={managerData.scopeTeamId}
          setScopeTeamId={managerData.setScopeTeamId}
          filteredTeamsByScope={managerData.filteredTeamsByScope}
          modalOpen={managerData.modalOpen}
          setModalOpen={managerData.setModalOpen}
          departmentForm={managerData.departmentForm}
          setDepartmentForm={managerData.setDepartmentForm}
          teamForm={managerData.teamForm}
          setTeamForm={managerData.setTeamForm}
          learnerForm={managerData.learnerForm}
          setLearnerForm={managerData.setLearnerForm}
          teamOptionsForLearner={managerData.teamOptionsForLearner}
          assignForm={managerData.assignForm}
          setAssignForm={managerData.setAssignForm}
          selectedTeamLearners={managerData.selectedTeamLearners}
          pendingAction={managerData.pendingAction}
          courseQuery={managerData.courseQuery}
          setCourseQuery={managerData.setCourseQuery}
          pagedCourses={managerData.pagedCourses}
          onCoursePageChange={managerData.onCoursePageChange}
          onSearchCourses={managerData.searchCourses}
          onToggleMandatory={managerData.toggleMandatory}
          onClearSkillTags={managerData.clearSkillTags}
          onCreateDepartment={managerData.createDepartment}
          onCreateTeam={managerData.createTeam}
          onCreateLearner={managerData.createLearner}
          onCreateAssignment={managerData.createAssignment}
          pagedManagerAssignments={managerData.pagedManagerAssignments}
          setManagerAssignmentPage={managerData.setManagerAssignmentPage}
          progressEdits={managerData.progressEdits}
          setProgressEdits={managerData.setProgressEdits}
          onSaveProgress={managerData.saveProgress}
          onSyncUdemy={managerData.syncUdemyProgress}
          onSyncLinkedIn={managerData.syncLinkedInProgress}
          mandatoryComplianceRows={managerData.mandatoryComplianceRows}
          pagedMandatoryComplianceRows={managerData.pagedMandatoryComplianceRows}
          setMandatoryGapPage={managerData.setMandatoryGapPage}
          skillMatchRows={managerData.skillMatchRows}
          skillMatchLoading={managerData.skillMatchLoading}
          onAnalyzeSkillMatch={managerData.searchSkillMatch}
        />
      ) : (
        <main className="page learner-page">
          <LearnerPortal
            learnerPersona={learnerPortalData.learnerPersona}
            setLearnerPersona={learnerPortalData.setLearnerPersona}
            teamManagerTeamId={learnerPortalData.teamManagerTeamId}
            setTeamManagerTeamId={learnerPortalData.setTeamManagerTeamId}
            teams={managerData.teams}
            teamMembers={learnerPortalData.teamMembers}
            teamManagerDashboard={learnerPortalData.teamManagerDashboard}
            teamManagerAssignments={learnerPortalData.teamManagerAssignments}
            pagedTeamManagerAssignments={learnerPortalData.pagedTeamManagerAssignments}
            teamManagerAssignmentPage={learnerPortalData.teamManagerAssignmentPage}
            onTeamManagerAssignmentPageChange={learnerPortalData.setTeamManagerAssignmentPage}
            teamManagerMandatoryGaps={learnerPortalData.teamManagerMandatoryGaps}
            pagedTeamManagerMandatoryGaps={learnerPortalData.pagedTeamManagerMandatoryGaps}
            teamManagerMandatoryGapsPage={learnerPortalData.teamManagerMandatoryGapsPage}
            onTeamManagerMandatoryGapsPageChange={learnerPortalData.setTeamManagerMandatoryGapsPage}
            onRefreshTeamView={learnerPortalData.loadTeamPersonaData}
            onRefreshUdemy={managerData.syncUdemyProgress}
            onRefreshLinkedIn={managerData.syncLinkedInProgress}
            individualEmployeeCode={learnerPortalData.individualEmployeeCode}
            setIndividualEmployeeCode={learnerPortalData.setIndividualEmployeeCode}
            individualLearner={learnerPortalData.individualLearner}
            onStartIndividualSession={learnerPortalData.startIndividualSession}
            onSignOut={learnerPortalData.signOutIndividualSession}
            learnerAssignments={learnerPortalData.learnerAssignments}
            pagedLearnerAssignments={learnerPortalData.pagedLearnerAssignments}
            learnerAssignmentPage={learnerPortalData.learnerAssignmentPage}
            onLearnerAssignmentPageChange={learnerPortalData.setLearnerAssignmentPage}
          />
        </main>
      )}
    </div>
  )
}

export default App
