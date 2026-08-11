import React from 'react'
import { useTranslation } from 'react-i18next'

export function PaginationControls({ page, totalPages, onPageChange }) {
  const { t } = useTranslation()

  if (totalPages <= 1) {
    return null
  }

  const pages = []
  const from = Math.max(1, page - 2)
  const to = Math.min(totalPages, page + 2)

  for (let index = from; index <= to; index += 1) {
    pages.push(index)
  }

  return (
    <div className="pager" role="navigation" aria-label={t('pagination.ariaLabel')}>
      <button type="button" disabled={page === 1} onClick={() => onPageChange(page - 1)}>
        {t('pagination.prev')}
      </button>
      {pages.map((value) => (
        <button
          key={value}
          type="button"
          className={value === page ? 'active' : ''}
          onClick={() => onPageChange(value)}
        >
          {value}
        </button>
      ))}
      <button type="button" disabled={page === totalPages} onClick={() => onPageChange(page + 1)}>
        {t('pagination.next')}
      </button>
    </div>
  )
}
