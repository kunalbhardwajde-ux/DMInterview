import { describe, expect, it } from 'vitest'
import { groupSkillMatchRows, normalizeSkillInput } from './skillMatchUtils'

describe('skillMatchUtils', () => {
  it('normalizes skill input into a compact comma-separated string', () => {
    expect(normalizeSkillInput(' cloud, security, ai , ')).toBe('cloud, security, ai')
  })

  it('merges rows for the same learner and preserves the strongest tier', () => {
    const rows = [
      {
        learnerId: 'l-1',
        learnerName: 'Asha',
        matchScore: 8,
        matchedCourses: 2,
        completedCourses: 3,
        priorityTier: 'Low',
        matchedSkillKeywords: 'cloud',
        topMatchedCourses: 'Cloud Basics',
      },
      {
        learnerId: 'l-1',
        learnerName: 'Asha',
        matchScore: 16,
        matchedCourses: 4,
        completedCourses: 6,
        priorityTier: 'High',
        matchedSkillKeywords: 'security, ai',
        topMatchedCourses: 'Security Essentials | AI Lab',
      },
    ]

    const grouped = groupSkillMatchRows(rows)

    expect(grouped).toHaveLength(1)
    expect(grouped[0].priorityTier).toBe('High')
    expect(grouped[0].matchedSkillKeywords).toBe('cloud, security, ai')
    expect(grouped[0].topMatchedCourses).toBe('Cloud Basics | Security Essentials | AI Lab')
  })
})
