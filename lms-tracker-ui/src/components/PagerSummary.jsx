import React from 'react'
import { useTranslation } from 'react-i18next'

export function PagerSummary({ pagedResult }) {
  const { t } = useTranslation()
  const { totalItems, startIndex, pageSize } = pagedResult

  return (
    <div className="pager-meta">
      {t('pagination.showing', {
        start: totalItems === 0 ? 0 : startIndex + 1,
        end: Math.min(startIndex + pageSize, totalItems),
        total: totalItems,
      })}
    </div>
  )
}
