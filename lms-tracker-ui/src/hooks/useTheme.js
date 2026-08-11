import { useEffect, useState } from 'react'

export const themes = [
  { id: 'atlas', preview: { bg: '#edf2f7', panel: '#ffffff', brand: '#1d5fd6' } },
  { id: 'copper', preview: { bg: '#f6efe6', panel: '#fffdf8', brand: '#b35d26' } },
  { id: 'mint', preview: { bg: '#eaf7f2', panel: '#f9fffc', brand: '#0f8f72' } },
]

function getInitialTheme() {
  const saved = localStorage.getItem('lms-theme')
  return themes.some((theme) => theme.id === saved) ? saved : 'atlas'
}

export function useTheme() {
  const [theme, setTheme] = useState(getInitialTheme)
  const [themePreview, setThemePreview] = useState('')
  const [themeMenuOpen, setThemeMenuOpen] = useState(false)
  const effectiveTheme = themePreview || theme

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', effectiveTheme)
  }, [effectiveTheme])

  useEffect(() => {
    localStorage.setItem('lms-theme', theme)
  }, [theme])

  useEffect(() => {
    if (!themeMenuOpen && themePreview) {
      setThemePreview('')
    }
  }, [themeMenuOpen, themePreview])

  function applyTheme(themeId) {
    setTheme(themeId)
    setThemePreview('')
    setThemeMenuOpen(false)
  }

  return {
    theme,
    effectiveTheme,
    themePreview,
    setThemePreview,
    themeMenuOpen,
    setThemeMenuOpen,
    applyTheme,
  }
}
