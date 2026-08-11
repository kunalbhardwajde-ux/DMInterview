import React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { LoginGate } from './LoginGate'

describe('LoginGate', () => {
  it('calls onLogin with the trimmed access code on submit (Manager role, default)', async () => {
    const user = userEvent.setup()
    const onLogin = vi.fn(async () => {})
    render(<LoginGate onLogin={onLogin} />)

    await user.type(screen.getByPlaceholderText('Access code'), '  secret-code  ')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    await waitFor(() => {
      expect(onLogin).toHaveBeenCalledWith({ accessCode: 'secret-code' })
    })
  })

  it('calls onLogin with the employee code after switching to the Learner role', async () => {
    const user = userEvent.setup()
    const onLogin = vi.fn(async () => {})
    render(<LoginGate onLogin={onLogin} />)

    await user.click(screen.getByRole('button', { name: 'Learner' }))
    await user.type(screen.getByPlaceholderText('Employee code (e.g., EMP1001)'), '  EMP123  ')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    await waitFor(() => {
      expect(onLogin).toHaveBeenCalledWith({ employeeCode: 'EMP123' })
    })
  })

  it('shows an error message when onLogin rejects', async () => {
    const user = userEvent.setup()
    const onLogin = vi.fn(async () => {
      throw new Error('Invalid access code.')
    })
    render(<LoginGate onLogin={onLogin} />)

    await user.type(screen.getByPlaceholderText('Access code'), 'wrong-code')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    expect(await screen.findByText('Invalid access code.')).toBeInTheDocument()
  })

  it('disables submit until a credential is entered', () => {
    render(<LoginGate onLogin={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Sign In' })).toBeDisabled()
  })

  it('clears the field and switches placeholder when toggling roles', async () => {
    const user = userEvent.setup()
    render(<LoginGate onLogin={vi.fn()} />)

    await user.type(screen.getByPlaceholderText('Access code'), 'partial-input')
    await user.click(screen.getByRole('button', { name: 'Learner' }))

    expect(screen.getByPlaceholderText('Employee code (e.g., EMP1001)')).toHaveValue('')
  })
})
