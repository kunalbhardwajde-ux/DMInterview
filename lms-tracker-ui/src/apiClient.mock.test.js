import { describe, it, expect } from 'vitest'
import { apiConfig, apiRequest, apiRequestPage } from './apiClient'

describe('apiClient mock backend', () => {
  it('runs in mock mode for this test environment', () => {
    expect(apiConfig.useMockApi).toBe(true)
  })

  it('creates a department when name and code are valid', async () => {
    const department = await apiRequest('/departments', {
      method: 'POST',
      body: JSON.stringify({ name: 'Quality Assurance', code: 'qa-1' }),
    })

    expect(department.name).toBe('Quality Assurance')
    expect(department.code).toBe('QA-1')
    expect(department.id).toBeTruthy()
  })

  it('rejects creating a department when the code already exists', async () => {
    await apiRequest('/departments', {
      method: 'POST',
      body: JSON.stringify({ name: 'Finance', code: 'fin-dup' }),
    })

    await expect(
      apiRequest('/departments', {
        method: 'POST',
        body: JSON.stringify({ name: 'Finance Two', code: 'fin-dup' }),
      }),
    ).rejects.toThrow(/already exists/i)
  })

  it('rejects creating a department when name or code is missing', async () => {
    await expect(
      apiRequest('/departments', {
        method: 'POST',
        body: JSON.stringify({ name: '', code: 'X1' }),
      }),
    ).rejects.toThrow(/required/i)
  })

  it('lists departments sorted by name', async () => {
    const departments = await apiRequest('/departments')

    expect(Array.isArray(departments)).toBe(true)
    const names = departments.map((d) => d.name)
    expect(names).toEqual([...names].sort((a, b) => a.localeCompare(b)))
  })

  it('returns the full course list with no total when paging params are omitted', async () => {
    const courses = await apiRequest('/courses?query=')

    expect(Array.isArray(courses)).toBe(true)
    expect(courses.length).toBeGreaterThan(8)
  })

  it('pages the course catalog server-side and reports the pre-paging total via apiRequestPage', async () => {
    const allCourses = await apiRequest('/courses?query=')

    const firstPage = await apiRequestPage('/courses?query=', { page: 1, pageSize: 8 })
    expect(firstPage.items).toHaveLength(8)
    expect(firstPage.totalCount).toBe(allCourses.length)

    const secondPage = await apiRequestPage('/courses?query=', { page: 2, pageSize: 8 })
    expect(secondPage.items).toHaveLength(8)
    expect(secondPage.items[0].id).not.toBe(firstPage.items[0].id)
    expect(secondPage.totalCount).toBe(allCourses.length)
  })

  it('returns the full team list with no total when paging params are omitted', async () => {
    const teams = await apiRequest('/teams')

    expect(Array.isArray(teams)).toBe(true)
    expect(teams.length).toBeGreaterThan(8)
  })

  it('pages the teams directory server-side and reports the pre-paging total via apiRequestPage', async () => {
    const allTeams = await apiRequest('/teams')

    const firstPage = await apiRequestPage('/teams', { page: 1, pageSize: 8 })
    expect(firstPage.items).toHaveLength(8)
    expect(firstPage.totalCount).toBe(allTeams.length)

    const secondPage = await apiRequestPage('/teams', { page: 2, pageSize: 8 })
    expect(secondPage.items.length).toBeGreaterThan(0)
    expect(secondPage.items[0].id).not.toBe(firstPage.items[0].id)
    expect(secondPage.totalCount).toBe(allTeams.length)
  })
})
