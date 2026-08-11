import React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'

const profile = {
  id: 'learner-1',
  employeeCode: 'EMP1001',
  name: 'Asha Rao',
  designation: 'Software Engineer',
  teamName: 'Platform',
}

const allAssignments = Array.from({ length: 12 }, (_, index) => ({
  id: `a-${index + 1}`,
  learnerId: 'learner-1',
  courseTitle: `Course ${index + 1}`,
  provider: 'Udemy',
  launchUrl: `https://learning.example.com/${index + 1}`,
  accessType: 'Permanent',
  dueDate: null,
  progressPercent: 0,
  status: 'NotStarted',
}))

vi.mock('../../apiClient', () => ({
  apiRequest: vi.fn(async (path) => {
    if (path === '/learners/me') {
      return profile
    }
    return []
  }),
  apiRequestPage: vi.fn(async (path, { page = 1, pageSize = 8 } = {}) => {
    if (path === '/assignments/mine') {
      const start = (page - 1) * pageSize
      return { items: allAssignments.slice(start, start + pageSize), totalCount: allAssignments.length }
    }
    return { items: [], totalCount: 0 }
  }),
}))

import { LearnerSelfService } from './LearnerSelfService'

describe('LearnerSelfService', () => {
  it('loads and shows the learner profile and their own paginated assignments only', async () => {
    render(<LearnerSelfService onSignOut={vi.fn()} />)

    await waitFor(() => {
      expect(screen.getByText('Asha Rao')).toBeInTheDocument()
    })

    expect(screen.getByText('EMP1001')).toBeInTheDocument()
    expect(screen.getByText('Platform')).toBeInTheDocument()
    expect(screen.getByText('Course 1')).toBeInTheDocument()
    expect(screen.queryByText('Course 9')).not.toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => {
      expect(screen.getByText('Course 9')).toBeInTheDocument()
    })
    expect(screen.queryByText('Course 1')).not.toBeInTheDocument()
  })

  it('calls onSignOut when the sign-out button is clicked', async () => {
    const onSignOut = vi.fn()
    const user = userEvent.setup()
    render(<LearnerSelfService onSignOut={onSignOut} />)

    await waitFor(() => {
      expect(screen.getByText('Asha Rao')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Sign Out' }))
    expect(onSignOut).toHaveBeenCalledTimes(1)
  })
})
