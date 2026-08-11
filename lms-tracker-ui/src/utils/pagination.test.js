import { describe, expect, it } from 'vitest'
import { paginate } from './pagination'

describe('paginate', () => {
  it('returns page metadata and items for a middle page', () => {
    const items = Array.from({ length: 25 }, (_, i) => i + 1)
    const result = paginate(items, 2, 10)

    expect(result.items).toEqual([11, 12, 13, 14, 15, 16, 17, 18, 19, 20])
    expect(result.page).toBe(2)
    expect(result.totalPages).toBe(3)
    expect(result.totalItems).toBe(25)
    expect(result.startIndex).toBe(10)
  })

  it('clamps out-of-range page values', () => {
    const items = [1, 2, 3]
    const result = paginate(items, 99, 2)

    expect(result.page).toBe(2)
    expect(result.items).toEqual([3])
  })
})
