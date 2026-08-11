import { useEffect, useState } from 'react'
import { getAuthToken, getAuthRole, getTokenExpiresAt, login as apiLogin, clearAuthToken, onAuthExpired } from '../apiClient'

// The 8-hour token dies silently otherwise - the user's first signal would be a generic failed
// request. Warn a few minutes out instead, while there's still time to save work and re-auth.
const WARNING_WINDOW_MINUTES = 5
const CHECK_INTERVAL_MS = 30 * 1000

export function useAuthSession() {
  const [authToken, setAuthTokenState] = useState(() => getAuthToken())
  const [authRole, setAuthRoleState] = useState(() => getAuthRole())
  const [sessionExpiringSoon, setSessionExpiringSoon] = useState(false)

  // Mock mode never has a token to lose, so this listener is inert there - real mode uses it to
  // bounce back to the login gate when any request discovers the token was rejected (expired,
  // revoked, or never valid), no matter which of the app's many data-loading call sites hit it.
  useEffect(() => onAuthExpired(() => {
    setAuthTokenState(null)
    setAuthRoleState(null)
    setSessionExpiringSoon(false)
  }), [])

  useEffect(() => {
    if (!authToken) {
      setSessionExpiringSoon(false)
      return
    }

    const checkExpiry = () => {
      const expiresAt = getTokenExpiresAt()
      if (!expiresAt) {
        setSessionExpiringSoon(false)
        return
      }

      const msRemaining = new Date(expiresAt).getTime() - Date.now()
      setSessionExpiringSoon(msRemaining > 0 && msRemaining <= WARNING_WINDOW_MINUTES * 60 * 1000)
    }

    checkExpiry()
    const intervalId = setInterval(checkExpiry, CHECK_INTERVAL_MS)
    return () => clearInterval(intervalId)
  }, [authToken])

  async function handleLogin(credential) {
    await apiLogin(credential)
    setAuthTokenState(getAuthToken())
    setAuthRoleState(getAuthRole())
  }

  function handleSignOut() {
    clearAuthToken()
    setAuthTokenState(null)
    setAuthRoleState(null)
    setSessionExpiringSoon(false)
  }

  return {
    authToken,
    authRole,
    sessionExpiringSoon,
    warningWindowMinutes: WARNING_WINDOW_MINUTES,
    handleLogin,
    handleSignOut,
  }
}
