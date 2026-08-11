import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'

/// Full-screen gate shown in real (non-mock) API mode before any token exists. Not shown in
/// mock mode at all - mock mode never talks to a real server, so there's nothing to authenticate.
/// Two roles share this one form: Manager (shared access code, broad access) and Learner
/// (per-employee code, read-only access to just that learner's own data) - see AuthModule.cs.
export function LoginGate({ onLogin }) {
  const { t } = useTranslation()
  const [role, setRole] = useState('manager')
  const [credentialValue, setCredentialValue] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  function switchRole(nextRole) {
    setRole(nextRole)
    setCredentialValue('')
    setError('')
  }

  async function handleSubmit(event) {
    event.preventDefault()
    const trimmedValue = credentialValue.trim()
    if (!trimmedValue || submitting) {
      return
    }

    setSubmitting(true)
    setError('')
    try {
      await onLogin(role === 'manager' ? { accessCode: trimmedValue } : { employeeCode: trimmedValue })
    } catch (loginError) {
      setError(loginError?.message || t('auth.invalidCode'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="page-shell login-gate">
      <form className="login-gate-card" onSubmit={handleSubmit}>
        <h1>{t('auth.title')}</h1>
        <div className="persona-switch" role="group" aria-label={t('auth.title')}>
          <button type="button" className={role === 'manager' ? 'active' : ''} onClick={() => switchRole('manager')}>
            {t('auth.roleManager')}
          </button>
          <button type="button" className={role === 'learner' ? 'active' : ''} onClick={() => switchRole('learner')}>
            {t('auth.roleLearner')}
          </button>
        </div>
        <p>{role === 'manager' ? t('auth.hint') : t('auth.learnerHint')}</p>
        <input
          // Manager access codes are a shared secret (masked); a Learner's employee code is an
          // identity lookup, not a secret - masking it would just make it harder to type correctly.
          type={role === 'manager' ? 'password' : 'text'}
          value={credentialValue}
          onChange={(event) => setCredentialValue(event.target.value)}
          placeholder={role === 'manager' ? t('auth.accessCodePlaceholder') : t('auth.employeeCodePlaceholder')}
          aria-label={role === 'manager' ? t('auth.accessCodePlaceholder') : t('auth.employeeCodePlaceholder')}
          autoFocus
        />
        {error ? <div className="error-box">{error}</div> : null}
        <button type="submit" disabled={submitting || !credentialValue.trim()}>
          {submitting ? t('auth.signingIn') : t('auth.signIn')}
        </button>
      </form>
    </div>
  )
}
