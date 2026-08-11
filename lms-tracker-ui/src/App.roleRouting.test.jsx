import React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

// This file exists specifically to close a gap the "two employee-code flows" review finding
// called out: App.jsx's real-mode routing decision (authRole === 'Learner' -> LearnerSelfService,
// otherwise -> the Manager UI) had zero test coverage. LearnerSelfService itself was already
// tested standalone; the wiring that decides *when* it renders instead of the Manager UI was not.
let mockRole = 'Manager'

vi.mock('./apiClient', () => ({
  apiConfig: { useMockApi: false },
  getAuthToken: vi.fn(() => 'a-real-looking-token'),
  getAuthRole: vi.fn(() => mockRole),
  getTokenExpiresAt: vi.fn(() => null),
  login: vi.fn(async () => {}),
  clearAuthToken: vi.fn(),
  onAuthExpired: vi.fn(() => () => {}),
  apiRequest: vi.fn(async (path) => {
    if (path === '/learners/me') {
      return { id: 'learner-1', employeeCode: 'EMP1001', name: 'Asha Rao', designation: 'Engineer', teamName: 'Platform' }
    }

    return []
  }),
  apiRequestPage: vi.fn(async () => ({ items: [], totalCount: 0 })),
}))

import App from './App'

describe('App real-mode role routing', () => {
  beforeEach(() => {
    mockRole = 'Manager'
  })

  it('renders the Learner self-service page, not the Manager UI, when authRole is Learner', async () => {
    mockRole = 'Learner'
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('My Learning Dashboard')).toBeInTheDocument()
    })

    expect(screen.getByText('Asha Rao')).toBeInTheDocument()
    expect(screen.queryByText('Course Catalog')).not.toBeInTheDocument()
    expect(screen.queryByText('Teams Directory')).not.toBeInTheDocument()
  })

  it('renders the Manager UI, not the Learner self-service page, when authRole is Manager', async () => {
    mockRole = 'Manager'
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Course Catalog')).toBeInTheDocument()
    })

    expect(screen.queryByText('My Learning Dashboard')).not.toBeInTheDocument()
  })
})
