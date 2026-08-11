import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'

let mockToken = null
let mockExpiresAt = null
let mockRole = null

vi.mock('../apiClient', () => ({
  getAuthToken: vi.fn(() => mockToken),
  getTokenExpiresAt: vi.fn(() => mockExpiresAt),
  getAuthRole: vi.fn(() => mockRole),
  login: vi.fn(async () => {
    mockToken = 'token-123'
    mockRole = 'Learner'
    return mockRole
  }),
  clearAuthToken: vi.fn(() => {
    mockToken = null
    mockExpiresAt = null
    mockRole = null
  }),
  onAuthExpired: vi.fn(() => () => {}),
}))

import { useAuthSession } from './useAuthSession'

describe('useAuthSession', () => {
  beforeEach(() => {
    mockToken = 'token-123'
    mockExpiresAt = null
    mockRole = 'Manager'
  })

  it('does not warn when the token has plenty of time left', () => {
    mockExpiresAt = new Date(Date.now() + 60 * 60 * 1000).toISOString()
    const { result, unmount } = renderHook(() => useAuthSession())

    expect(result.current.sessionExpiringSoon).toBe(false)
    unmount()
  })

  it('warns once the token is inside the warning window', () => {
    mockExpiresAt = new Date(Date.now() + 2 * 60 * 1000).toISOString()
    const { result, unmount } = renderHook(() => useAuthSession())

    expect(result.current.sessionExpiringSoon).toBe(true)
    unmount()
  })

  it('clears the warning on sign out', () => {
    mockExpiresAt = new Date(Date.now() + 2 * 60 * 1000).toISOString()
    const { result, unmount } = renderHook(() => useAuthSession())
    expect(result.current.sessionExpiringSoon).toBe(true)

    act(() => {
      result.current.handleSignOut()
    })

    expect(result.current.sessionExpiringSoon).toBe(false)
    expect(result.current.authToken).toBe(null)
    unmount()
  })

  it('picks up the granted role after login and clears it on sign out', async () => {
    mockToken = null
    mockRole = null
    const { result, unmount } = renderHook(() => useAuthSession())
    expect(result.current.authRole).toBe(null)

    await act(async () => {
      await result.current.handleLogin({ employeeCode: 'EMP123' })
    })

    expect(result.current.authRole).toBe('Learner')

    act(() => {
      result.current.handleSignOut()
    })

    expect(result.current.authRole).toBe(null)
    unmount()
  })
})
