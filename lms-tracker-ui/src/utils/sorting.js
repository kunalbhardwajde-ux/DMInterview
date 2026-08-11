import { useCallback, useState } from 'react'

// Numeric-aware compare: numbers compare numerically, everything else compares as a
// locale-aware, numeric-aware string (so "Course 2" sorts before "Course 10", not after).
// Nulls/undefined always sort last, regardless of direction - a missing value isn't
// meaningfully "less than" or "greater than" a real one.
export function compareValues(a, b) {
  if (a == null && b == null) {
    return 0
  }
  if (a == null) {
    return 1
  }
  if (b == null) {
    return -1
  }
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b
  }

  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' })
}

// Sorts a copy of `rows` by `sortKey`/`sortDirection`. `accessor(row, sortKey)` lets a caller
// derive the comparable value when it isn't just `row[sortKey]` (e.g. mapping a status enum to
// a display label first). Returns `rows` unchanged (same reference) when there's no active sort,
// so callers can skip re-deriving downstream memoized values for the common "unsorted" case.
export function sortRows(rows, sortKey, sortDirection, accessor) {
  if (!sortKey) {
    return rows
  }

  const direction = sortDirection === 'desc' ? -1 : 1
  const getValue = accessor ?? ((row) => row[sortKey])

  return [...rows].sort((a, b) => compareValues(getValue(a, sortKey), getValue(b, sortKey)) * direction)
}

// Tracks which column a table is sorted by. Clicking the active column's header flips direction;
// clicking a different column switches to it, ascending. `initialKey` lets a table keep its
// existing default ordering (e.g. Skill Match's match-score-descending) until the user picks a
// column themselves.
export function useSortState(initialKey = '', initialDirection = 'asc') {
  const [sort, setSort] = useState({ key: initialKey, direction: initialDirection })

  const requestSort = useCallback((key) => {
    setSort((prev) => ({
      key,
      direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
    }))
  }, [])

  return { sortKey: sort.key, sortDirection: sort.direction, requestSort }
}
