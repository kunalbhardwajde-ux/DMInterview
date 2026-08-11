import React from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect } from 'vitest'

const mandatoryRows = Array.from({ length: 23 }, (_, index) => ({
  learnerId: `learner-${index + 1}`,
  employeeCode: `EMPM${String(index + 1).padStart(3, '0')}`,
  learnerName: `Learner ${index + 1}`,
  departmentName: 'Engineering',
  teamName: 'Platform',
  pendingMandatoryCourses: (index % 3) + 1,
  pendingCourseTitles: 'Security Essentials, Data Privacy Basics',
}))

const courseRows = Array.from({ length: 15 }, (_, index) => ({
  id: `c-${index + 1}`,
  title: `Course ${index + 1}`,
  provider: 'Udemy',
  launchUrl: `https://learning.example.com/c-${index + 1}`,
  isMandatory: false,
  skillTags: index === 0 ? ['cloud', 'aws'] : [],
}))

const teamRows = Array.from({ length: 12 }, (_, index) => ({
  id: `t-${index + 1}`,
  name: `Team ${index + 1}`,
  departmentId: 'd-1',
  departmentName: 'Engineering',
  managerName: `Manager ${index + 1}`,
  managerEmail: `manager${index + 1}@company.local`,
}))

const assignmentRows = Array.from({ length: 25 }, (_, index) => ({
  id: `a-${index + 1}`,
  learnerId: `learner-${index + 1}`,
  employeeCode: `EMPA${String(index + 1).padStart(3, '0')}`,
  learnerName: `Assignee ${index + 1}`,
  departmentId: 'd-1',
  departmentName: 'Engineering',
  teamName: 'Platform',
  courseId: `c-${(index % courseRows.length) + 1}`,
  courseTitle: `Course ${(index % courseRows.length) + 1}`,
  provider: 'Udemy',
  launchUrl: 'https://learning.example.com',
  accessType: 'Permanent',
  dueDate: null,
  progressPercent: 0,
  status: 'NotStarted',
}))

vi.mock('./apiClient', () => ({
  apiConfig: { useMockApi: true },
  getAuthToken: vi.fn(() => null),
  getAuthRole: vi.fn(() => null),
  getTokenExpiresAt: vi.fn(() => null),
  login: vi.fn(async () => {}),
  clearAuthToken: vi.fn(),
  onAuthExpired: vi.fn(() => () => {}),
  apiRequest: vi.fn(async (path) => {
    if (path.startsWith('/courses')) {
      return courseRows
    }

    if (path === '/departments') {
      return [{ id: 'd-1', name: 'Engineering', code: 'ENG' }]
    }

    if (path === '/teams') {
      return teamRows
    }

    if (path === '/learners') {
      return []
    }

    if (path.startsWith('/assignments')) {
      return assignmentRows
    }

    if (path.startsWith('/dashboard')) {
      return {
        departments: 1,
        teams: 1,
        learners: 23,
        assignments: assignmentRows.length,
        completed: 0,
        inProgress: 0,
        notStarted: assignmentRows.length,
        overdue: 0,
        completionRate: 0,
      }
    }

    if (path.startsWith('/reports/mandatory-compliance')) {
      return mandatoryRows
    }

    return []
  }),
  apiRequestPage: vi.fn(async (path, { page = 1, pageSize = 8 } = {}) => {
    if (path.startsWith('/courses')) {
      const start = (page - 1) * pageSize
      return { items: courseRows.slice(start, start + pageSize), totalCount: courseRows.length }
    }

    if (path.startsWith('/teams')) {
      const start = (page - 1) * pageSize
      return { items: teamRows.slice(start, start + pageSize), totalCount: teamRows.length }
    }

    return { items: [], totalCount: 0 }
  }),
}))

import App from './App'
import { apiRequest } from './apiClient'

describe('App Mandatory Course Gaps pagination', () => {
  it('shows paged rows and navigates to next page', async () => {
    const user = userEvent.setup()
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Mandatory Course Gaps')).toBeInTheDocument()
      expect(screen.getByText('EMPM001')).toBeInTheDocument()
    })

    expect(screen.queryByText('EMPM011')).not.toBeInTheDocument()

    const section = screen.getByText('Mandatory Course Gaps').closest('section')
    const nextButton = within(section).getByRole('button', { name: 'Next' })
    await user.click(nextButton)

    await waitFor(() => {
      expect(screen.getByText('EMPM011')).toBeInTheDocument()
    })

    expect(screen.queryByText('EMPM001')).not.toBeInTheDocument()
    const pagerMetaMatches = screen.getAllByText((_, element) => element?.textContent?.includes('of 23') ?? false)
    expect(pagerMetaMatches.length).toBeGreaterThan(0)
  })
})

describe('App Course Catalog pagination', () => {
  // The course title and provider render as sibling text/<em> nodes ("Course 1" + " " + "(Udemy)"),
  // so a plain string match against a single node won't find it - match on the full text of the
  // specific .course-line span instead, and require the trailing "(Udemy)" so "Course 1" doesn't
  // also match "Course 10"/"Course 11"/etc as a substring.
  const courseLine = (title) => (_content, element) =>
    (element?.className === 'course-line') && element.textContent.trim() === `${title} (Udemy)`

  it('shows paged courses and navigates to next page', async () => {
    const user = userEvent.setup()
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Course Catalog')).toBeInTheDocument()
      expect(screen.getByText(courseLine('Course 1'))).toBeInTheDocument()
    })

    expect(screen.queryByText(courseLine('Course 9'))).not.toBeInTheDocument()

    const section = screen.getByText('Course Catalog').closest('section')
    const nextButton = within(section).getByRole('button', { name: 'Next' })
    await user.click(nextButton)

    await waitFor(() => {
      expect(screen.getByText(courseLine('Course 9'))).toBeInTheDocument()
    })

    expect(screen.queryByText(courseLine('Course 1'))).not.toBeInTheDocument()
  })
})

describe('App Course Catalog AI skill tags', () => {
  it('shows AI-suggested tags for a tagged course and clears them via the override button', async () => {
    const user = userEvent.setup()
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Course Catalog')).toBeInTheDocument()
      expect(screen.getByText('cloud')).toBeInTheDocument()
    })

    expect(screen.getByText('aws')).toBeInTheDocument()
    expect(screen.getByText('AI-suggested • high confidence')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Clear tags' }))

    await waitFor(() => {
      expect(
        apiRequest.mock.calls.some(
          ([path, options]) => path === '/courses/c-1/skill-tags' && options?.method === 'DELETE',
        ),
      ).toBe(true)
    })
  })
})

describe('App Progress Tracker (manager assignments) pagination', () => {
  it('shows paged assignments and navigates to next page', async () => {
    const user = userEvent.setup()
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Progress Tracker')).toBeInTheDocument()
      expect(screen.getByText('EMPA001')).toBeInTheDocument()
    })

    expect(screen.queryByText('EMPA011')).not.toBeInTheDocument()

    const section = screen.getByText('Progress Tracker').closest('section')
    const nextButton = within(section).getByRole('button', { name: 'Next' })
    await user.click(nextButton)

    await waitFor(() => {
      expect(screen.getByText('EMPA011')).toBeInTheDocument()
    })

    expect(screen.queryByText('EMPA001')).not.toBeInTheDocument()
  })
})

describe('App Teams Directory pagination', () => {
  // Team names also appear as <option> text in the scope-filter dropdown above this section
  // (rendered from the same, full `teams` list), so queries must be scoped to this section's
  // own table rather than the whole document.
  it('shows paged teams and navigates to next page', async () => {
    const user = userEvent.setup()
    render(<App />)

    let section
    await waitFor(() => {
      section = screen.getByText('Teams Directory').closest('section')
      expect(within(section).getByText('Team 1')).toBeInTheDocument()
    })

    expect(within(section).queryByText('Team 9')).not.toBeInTheDocument()

    const nextButton = within(section).getByRole('button', { name: 'Next' })
    await user.click(nextButton)

    await waitFor(() => {
      expect(within(section).getByText('Team 9')).toBeInTheDocument()
    })

    expect(within(section).queryByText('Team 1')).not.toBeInTheDocument()
  })
})
