import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { LearnerPortal } from './LearnerPortal'
import { paginate } from '../../utils/pagination'

const team = { id: 't-1', name: 'Platform', managerName: 'Sam', departmentId: 'd-1' }

function makeAssignments(count) {
  return Array.from({ length: count }, (_, index) => ({
    id: `a-${index + 1}`,
    employeeCode: `EMP${index + 1}`,
    learnerName: `Learner ${index + 1}`,
    courseTitle: `Course ${index + 1}`,
    provider: 'Udemy',
    accessType: 'Permanent',
    dueDate: null,
    progressPercent: 0,
    status: 'NotStarted',
    launchUrl: 'https://example.com',
  }))
}

function makeMandatoryGaps(count) {
  return Array.from({ length: count }, (_, index) => ({
    learnerId: `l-${index + 1}`,
    employeeCode: `EMP${index + 1}`,
    learnerName: `Learner ${index + 1}`,
    pendingMandatoryCourses: 1,
    pendingCourseTitles: 'Security Essentials',
  }))
}

function baseProps(overrides = {}) {
  return {
    learnerPersona: 'teamManager',
    setLearnerPersona: () => {},
    teamManagerTeamId: team.id,
    setTeamManagerTeamId: () => {},
    teams: [team],
    teamMembers: [],
    teamManagerDashboard: null,
    teamManagerAssignments: [],
    pagedTeamManagerAssignments: paginate([], 1, 10),
    teamManagerAssignmentPage: 1,
    onTeamManagerAssignmentPageChange: () => {},
    teamManagerMandatoryGaps: [],
    pagedTeamManagerMandatoryGaps: paginate([], 1, 10),
    teamManagerMandatoryGapsPage: 1,
    onTeamManagerMandatoryGapsPageChange: () => {},
    onRefreshTeamView: () => {},
    onRefreshUdemy: () => {},
    onRefreshLinkedIn: () => {},
    individualEmployeeCode: '',
    setIndividualEmployeeCode: () => {},
    individualLearner: null,
    onStartIndividualSession: () => {},
    onSignOut: () => {},
    learnerAssignments: [],
    pagedLearnerAssignments: paginate([], 1, 8),
    learnerAssignmentPage: 1,
    onLearnerAssignmentPageChange: () => {},
    ...overrides,
  }
}

describe('LearnerPortal pagination', () => {
  it('paginates the Team Course Progress table and calls back on Next', async () => {
    const user = userEvent.setup()
    const assignments = makeAssignments(15)
    const onPageChange = vi.fn()

    render(
      <LearnerPortal
        {...baseProps({
          teamManagerAssignments: assignments,
          pagedTeamManagerAssignments: paginate(assignments, 1, 10),
          onTeamManagerAssignmentPageChange: onPageChange,
        })}
      />,
    )

    expect(screen.getByText('EMP1')).toBeInTheDocument()
    expect(screen.queryByText('EMP11')).not.toBeInTheDocument()

    const nextButtons = screen.getAllByRole('button', { name: 'Next' })
    await user.click(nextButtons[0])

    expect(onPageChange).toHaveBeenCalledWith(2)
  })

  it('paginates the Team Mandatory Gaps table and calls back on Next', async () => {
    const user = userEvent.setup()
    const gaps = makeMandatoryGaps(15)
    const onPageChange = vi.fn()

    render(
      <LearnerPortal
        {...baseProps({
          teamManagerMandatoryGaps: gaps,
          pagedTeamManagerMandatoryGaps: paginate(gaps, 1, 10),
          onTeamManagerMandatoryGapsPageChange: onPageChange,
        })}
      />,
    )

    expect(screen.getByText('EMP1')).toBeInTheDocument()
    expect(screen.queryByText('EMP11')).not.toBeInTheDocument()

    const nextButtons = screen.getAllByRole('button', { name: 'Next' })
    expect(nextButtons.length).toBeGreaterThan(0)
    await user.click(nextButtons[nextButtons.length - 1])

    expect(onPageChange).toHaveBeenCalledWith(2)
  })

  it('paginates the individual learner My Assigned Courses table and calls back on Next', async () => {
    const user = userEvent.setup()
    const assignments = makeAssignments(10)
    const onPageChange = vi.fn()
    const individualLearner = { id: 'l-1', name: 'Ada Lovelace', employeeCode: 'EMP1001', designation: 'Engineer' }

    render(
      <LearnerPortal
        {...baseProps({
          learnerPersona: 'individual',
          individualLearner,
          learnerAssignments: assignments,
          pagedLearnerAssignments: paginate(assignments, 1, 8),
          onLearnerAssignmentPageChange: onPageChange,
        })}
      />,
    )

    expect(screen.getByText('Course 1')).toBeInTheDocument()
    expect(screen.queryByText('Course 9')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(onPageChange).toHaveBeenCalledWith(2)
  })
})
