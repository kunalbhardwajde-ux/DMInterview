import React, { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { normalizeSkillInput, groupSkillMatchRows } from './skillMatchUtils'
import { paginate } from '../../utils/pagination'
import { PaginationControls } from '../../components/PaginationControls'
import { PagerSummary } from '../../components/PagerSummary'

export function SkillMatchPanel({ rows, onAnalyze, loading = false }) {
  const { t } = useTranslation()
  const [skillSearchInput, setSkillSearchInput] = useState('cloud, security, ai')
  const [expandedLearnerId, setExpandedLearnerId] = useState('')
  const [page, setPage] = useState(1)

  const groupedRows = useMemo(() => groupSkillMatchRows(rows), [rows])
  const pagedRows = useMemo(() => paginate(groupedRows, page, 10), [groupedRows, page])

  function handleAnalyze() {
    onAnalyze(normalizeSkillInput(skillSearchInput))
  }

  function reset() {
    setSkillSearchInput('cloud, security, ai')
    onAnalyze('')
  }

  return (
    <section className="panel">
      <div className="panel-title-row">
        <h2>{t('skillMatch.title')}</h2>
        <p className="muted-inline">{t('skillMatch.description')}</p>
      </div>
      <p className="hint">{t('skillMatch.scaleHint')}</p>
      <div className="form compact-grid">
        <input
          value={skillSearchInput}
          onChange={(event) => setSkillSearchInput(event.target.value)}
          placeholder={t('skillMatch.inputPlaceholder')}
        />
        <button type="button" onClick={handleAnalyze} disabled={loading}>
          {loading ? t('skillMatch.analyzing') : t('skillMatch.analyze')}
        </button>
        <button type="button" className="ghost" onClick={reset}>
          {t('skillMatch.resetFilter')}
        </button>
      </div>

      {rows.length === 0 ? (
        <p className="hint">{t('skillMatch.emptyHint')}</p>
      ) : (
        <>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>{t('skillMatch.columns.priority')}</th>
                  <th>{t('skillMatch.columns.score')}</th>
                  <th>{t('common.empCode')}</th>
                  <th>{t('common.employee')}</th>
                  <th>{t('common.department')}</th>
                  <th>{t('common.team')}</th>
                  <th>{t('skillMatch.columns.completedCourses')}</th>
                  <th>{t('skillMatch.columns.matchedCourses')}</th>
                  <th>{t('skillMatch.columns.matchedSkills')}</th>
                  <th>{t('skillMatch.columns.missingSkills')}</th>
                  <th>{t('skillMatch.columns.topMatchedCourses')}</th>
                  <th>{t('skillMatch.columns.details')}</th>
                </tr>
              </thead>
              <tbody>
                {pagedRows.items.flatMap((row) => {
                  const isExpanded = expandedLearnerId === row.learnerId
                  return [
                    <tr key={row.learnerId}>
                      <td>{t(`enums.priorityTier.${row.priorityTier}`, row.priorityTier)}</td>
                      <td>{row.matchScore}</td>
                      <td>{row.employeeCode}</td>
                      <td>{row.learnerName}</td>
                      <td>{row.departmentName || t('common.na')}</td>
                      <td>{row.teamName || t('common.na')}</td>
                      <td>{row.completedCourses}</td>
                      <td>{row.matchedCourses}</td>
                      <td>{row.matchedSkillKeywords || t('common.na')}</td>
                      <td className={row.missingSkillKeywords ? 'skill-gap-cell' : undefined}>
                        {row.missingSkillKeywords || t('skillMatch.noGaps')}
                      </td>
                      <td>{row.topMatchedCourses || t('common.na')}</td>
                      <td>
                        <button
                          type="button"
                          className="ghost"
                          aria-expanded={isExpanded}
                          aria-controls={`skill-detail-${row.learnerId}`}
                          onClick={() => setExpandedLearnerId(isExpanded ? '' : row.learnerId)}
                        >
                          {isExpanded ? t('skillMatch.less') : t('skillMatch.more')}
                        </button>
                      </td>
                    </tr>,
                    isExpanded ? (
                      <tr key={`${row.learnerId}-detail`} id={`skill-detail-${row.learnerId}`} className="skill-detail-row">
                        <td colSpan={12}>
                          <div className="skill-detail-grid">
                            <div>
                              <strong>{t('skillMatch.allMatchedSkills')}</strong>{' '}
                              {row._allSkills && row._allSkills.length > 0 ? row._allSkills.join(', ') : t('common.na')}
                            </div>
                            <div>
                              <strong>{t('skillMatch.allMissingSkills')}</strong>{' '}
                              {row._allMissingSkills && row._allMissingSkills.length > 0 ? row._allMissingSkills.join(', ') : t('skillMatch.noGaps')}
                            </div>
                            <div>
                              <strong>{t('skillMatch.allMatchedCourses')}</strong>{' '}
                              {row._allCourses && row._allCourses.length > 0 ? row._allCourses.join(' | ') : t('common.na')}
                            </div>
                            <div>
                              <strong>{t('skillMatch.scoreBreakdown')}</strong>{' '}
                              {row.scoreBreakdown || t('common.na')}
                            </div>
                          </div>
                        </td>
                      </tr>
                    ) : null,
                  ]
                })}
              </tbody>
            </table>
          </div>

          <PagerSummary pagedResult={pagedRows} />
          <PaginationControls page={page} totalPages={pagedRows.totalPages} onPageChange={setPage} />
        </>
      )}
    </section>
  )
}
