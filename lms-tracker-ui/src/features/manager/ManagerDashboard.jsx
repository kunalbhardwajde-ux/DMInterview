import React from 'react'
import { useTranslation } from 'react-i18next'

export function ManagerDashboard({ dashboard }) {
  const { t } = useTranslation()

  return (
    <section className="stats-grid">
      <article className="stat-card">
        <h3>{t('manager.dashboard.departments')}</h3>
        <strong>{dashboard?.departments ?? 0}</strong>
      </article>
      <article className="stat-card">
        <h3>{t('manager.dashboard.teams')}</h3>
        <strong>{dashboard?.teams ?? 0}</strong>
      </article>
      <article className="stat-card">
        <h3>{t('manager.dashboard.learners')}</h3>
        <strong>{dashboard?.learners ?? 0}</strong>
      </article>
      <article className="stat-card">
        <h3>{t('manager.dashboard.assignments')}</h3>
        <strong>{dashboard?.assignments ?? 0}</strong>
      </article>
      <article className="stat-card">
        <h3>{t('common.completion')}</h3>
        <strong>{dashboard?.completionRate ?? 0}%</strong>
      </article>
      <article className="stat-card">
        <h3>{t('manager.dashboard.overdue')}</h3>
        <strong>{dashboard?.overdue ?? 0}</strong>
      </article>
    </section>
  )
}
