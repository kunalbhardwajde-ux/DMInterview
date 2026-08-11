import { useEffect, useState } from 'react'

function normalizePath(pathname) {
  if (pathname === '/manager' || pathname === '/learner') {
    return pathname
  }

  return '/manager'
}

export function useRoute() {
  const [route, setRoute] = useState(() => normalizePath(window.location.pathname))

  useEffect(() => {
    const path = normalizePath(window.location.pathname)
    if (path !== window.location.pathname) {
      window.history.replaceState({}, '', path)
    }
    setRoute(path)

    const onPopState = () => setRoute(normalizePath(window.location.pathname))
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  function goTo(path) {
    if (path === route) {
      return
    }

    window.history.pushState({}, '', path)
    setRoute(path)
  }

  return { route, goTo }
}
