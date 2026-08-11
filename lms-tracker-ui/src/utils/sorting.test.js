import { describe, it, expect } from 'vitest'
import { compareValues, sortRows } from './sorting'

describe('compareValues', () => {
  it('compares numbers numerically', () => {
    expect(compareValues(2, 10)).toBeLessThan(0)
    expect(compareValues(10, 2)).toBeGreaterThan(0)
  })

  it('compares strings numeric-aware, not lexicographically', () => {
    expect(compareValues('Course 2', 'Course 10')).toBeLessThan(0)
  })

  it('sorts nulls and undefined last regardless of which side they are on', () => {
    expect(compareValues(null, 'a')).toBeGreaterThan(0)
    expect(compareValues('a', null)).toBeLessThan(0)
    expect(compareValues(undefined, undefined)).toBe(0)
  })
})

describe('sortRows', () => {
  const rows = [
    { name: 'Charlie', score: 5 },
    { name: 'Alice', score: 20 },
    { name: 'Bob', score: 10 },
  ]

  it('returns the same array reference when there is no sort key', () => {
    expect(sortRows(rows, '', 'asc')).toBe(rows)
  })

  it('sorts ascending by default', () => {
    const sorted = sortRows(rows, 'name', 'asc')
    expect(sorted.map((r) => r.name)).toEqual(['Alice', 'Bob', 'Charlie'])
  })

  it('sorts descending when asked', () => {
    const sorted = sortRows(rows, 'score', 'desc')
    expect(sorted.map((r) => r.score)).toEqual([20, 10, 5])
  })

  it('does not mutate the original array', () => {
    const sorted = sortRows(rows, 'name', 'asc')
    expect(sorted).not.toBe(rows)
    expect(rows.map((r) => r.name)).toEqual(['Charlie', 'Alice', 'Bob'])
  })

  it('uses a custom accessor when provided, e.g. sorting by a derived label', () => {
    const statusRows = [{ status: 'InProgress' }, { status: 'Completed' }, { status: 'NotStarted' }]
    const rank = { NotStarted: 0, InProgress: 1, Completed: 2 }
    const sorted = sortRows(statusRows, 'status', 'asc', (row) => rank[row.status])
    expect(sorted.map((r) => r.status)).toEqual(['NotStarted', 'InProgress', 'Completed'])
  })
})
