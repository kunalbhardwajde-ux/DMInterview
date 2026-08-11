import React from 'react'

// A <th> whose whole cell is a sort click-target. aria-sort tells screen readers the current
// state (including "none" for sortable-but-inactive, which is a real ARIA value, not a gap);
// the arrow indicator gives the same information visually, faint when inactive so it doesn't
// compete with active columns but still hints the header is clickable.
export function SortableHeader({ label, sortKey, currentSortKey, currentSortDirection, onSort }) {
  const isActive = currentSortKey === sortKey
  const ariaSort = isActive ? (currentSortDirection === 'asc' ? 'ascending' : 'descending') : 'none'

  return (
    <th aria-sort={ariaSort}>
      <button type="button" className="sortable-header" onClick={() => onSort(sortKey)}>
        <span>{label}</span>
        <span className={isActive ? 'sort-indicator active' : 'sort-indicator'} aria-hidden="true">
          {isActive ? (currentSortDirection === 'asc' ? '▲' : '▼') : '⇅'}
        </span>
      </button>
    </th>
  )
}
