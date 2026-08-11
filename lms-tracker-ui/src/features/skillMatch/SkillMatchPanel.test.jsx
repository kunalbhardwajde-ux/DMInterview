import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { SkillMatchPanel } from './SkillMatchPanel'

const rows = [
  {
    learnerId: 'learner-1',
    employeeCode: 'EMP001',
    learnerName: 'Ada Lovelace',
    departmentName: 'Engineering',
    teamName: 'Platform',
    completedCourses: 4,
    matchedCourses: 2,
    matchedSkills: 2,
    matchScore: 14,
    priorityTier: 'Medium',
    matchedSkillKeywords: 'cloud, security',
    topMatchedCourses: 'Cloud Fundamentals | Applied Security Practices',
  },
]

describe('SkillMatchPanel', () => {
  it('shows the hint and hides the table when there are no rows', () => {
    render(<SkillMatchPanel rows={[]} onAnalyze={() => {}} />)

    expect(screen.getByText(/Run analysis to generate a prioritized interview shortlist/)).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('expands a row to show the full matched-skills/courses detail, then collapses it again', async () => {
    const user = userEvent.setup()
    render(<SkillMatchPanel rows={rows} onAnalyze={() => {}} />)

    const toggleButton = screen.getByRole('button', { name: 'More' })
    expect(toggleButton).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByText(/All Matched Skills/)).not.toBeInTheDocument()

    await user.click(toggleButton)

    expect(screen.getByRole('button', { name: 'Less' })).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText(/All Matched Skills/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Less' }))

    expect(screen.queryByText(/All Matched Skills/)).not.toBeInTheDocument()
  })

  it('normalizes and forwards the skill search input on Analyze', async () => {
    const user = userEvent.setup()
    const onAnalyze = vi.fn()
    render(<SkillMatchPanel rows={[]} onAnalyze={onAnalyze} />)

    const input = screen.getByPlaceholderText('Enter skills: cloud, security, ai')
    await user.clear(input)
    await user.type(input, ' cloud ,, security ')
    await user.click(screen.getByRole('button', { name: 'Analyze Skill Match' }))

    expect(onAnalyze).toHaveBeenCalledWith('cloud, security')
  })

  it('defaults to matchScore descending, then re-sorts client-side when a column header is clicked', async () => {
    const user = userEvent.setup()
    const multiRows = [
      { ...rows[0], learnerId: 'learner-2', learnerName: 'Bob Stone', employeeCode: 'EMP002', matchScore: 20 },
      { ...rows[0], learnerId: 'learner-1', learnerName: 'Ada Lovelace', employeeCode: 'EMP001', matchScore: 14 },
      { ...rows[0], learnerId: 'learner-3', learnerName: 'Charlie Kim', employeeCode: 'EMP003', matchScore: 5 },
    ]

    render(<SkillMatchPanel rows={multiRows} onAnalyze={() => {}} />)

    expect(screen.getAllByText(/Lovelace|Stone|Kim/).map((el) => el.textContent)).toEqual([
      'Bob Stone',
      'Ada Lovelace',
      'Charlie Kim',
    ])

    await user.click(screen.getByRole('button', { name: 'Employee' }))

    expect(screen.getAllByText(/Lovelace|Stone|Kim/).map((el) => el.textContent)).toEqual([
      'Ada Lovelace',
      'Bob Stone',
      'Charlie Kim',
    ])

    await user.click(screen.getByRole('button', { name: 'Employee' }))

    expect(screen.getAllByText(/Lovelace|Stone|Kim/).map((el) => el.textContent)).toEqual([
      'Charlie Kim',
      'Bob Stone',
      'Ada Lovelace',
    ])
  })
})
