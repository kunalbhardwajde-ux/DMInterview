export function normalizeSkillInput(value) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
    .join(', ')
}

function tokensFrom(value, separator) {
  return String(value || '')
    .split(separator)
    .map((item) => item.trim())
    .filter(Boolean)
}

export function groupSkillMatchRows(rows) {
  const byLearner = new Map()

  for (const row of rows) {
    const skillTokens = tokensFrom(row.matchedSkillKeywords, ',')
    const missingTokens = tokensFrom(row.missingSkillKeywords, ',')
    const courseTokens = tokensFrom(row.topMatchedCourses, '|')

    if (!byLearner.has(row.learnerId)) {
      byLearner.set(row.learnerId, {
        ...row,
        _allSkills: skillTokens,
        _allMissingSkills: missingTokens,
        _allCourses: courseTokens,
      })
      continue
    }

    const existing = byLearner.get(row.learnerId)
    const mergedSkills = [...new Set([...existing._allSkills, ...skillTokens])]
    const mergedCourses = [...new Set([...existing._allCourses, ...courseTokens])]
    // A skill only counts as still-missing for the merged learner if no contributing row
    // matched it - otherwise a skill "missing" in one scope but matched in another would
    // incorrectly show up as a gap after merging.
    const mergedMissingSkills = [...new Set([...existing._allMissingSkills, ...missingTokens])].filter(
      (skill) => !mergedSkills.includes(skill),
    )
    const tierRank = { Low: 0, Medium: 1, High: 2 }
    const betterTier = tierRank[row.priorityTier] > tierRank[existing.priorityTier]
    const betterScore = row.matchScore > existing.matchScore

    byLearner.set(row.learnerId, {
      ...existing,
      completedCourses: Math.max(existing.completedCourses, row.completedCourses),
      matchedCourses: Math.max(existing.matchedCourses, row.matchedCourses, mergedCourses.length),
      matchedSkills: Math.max(existing.matchedSkills, row.matchedSkills, mergedSkills.length),
      matchScore: Math.max(existing.matchScore, row.matchScore),
      priorityTier: betterTier ? row.priorityTier : existing.priorityTier,
      scoreBreakdown: betterScore ? row.scoreBreakdown : existing.scoreBreakdown,
      matchedSkillKeywords: mergedSkills.join(', '),
      missingSkillKeywords: mergedMissingSkills.join(', '),
      topMatchedCourses: mergedCourses.slice(0, 3).join(' | '),
      _allSkills: mergedSkills,
      _allMissingSkills: mergedMissingSkills,
      _allCourses: mergedCourses,
    })
  }

  return [...byLearner.values()].sort((a, b) => {
    if (b.matchScore !== a.matchScore) {
      return b.matchScore - a.matchScore
    }
    if (b.matchedCourses !== a.matchedCourses) {
      return b.matchedCourses - a.matchedCourses
    }
    if (b.completedCourses !== a.completedCourses) {
      return b.completedCourses - a.completedCourses
    }
    return a.learnerName.localeCompare(b.learnerName)
  })
}
