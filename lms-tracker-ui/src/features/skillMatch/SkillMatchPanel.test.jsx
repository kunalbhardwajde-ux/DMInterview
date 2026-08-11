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
})
