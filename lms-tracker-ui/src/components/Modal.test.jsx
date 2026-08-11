import React, { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { Modal } from './Modal'

describe('Modal', () => {
  it('focuses the first focusable element when it opens', () => {
    render(
      <Modal title="Add Employee" onClose={() => {}}>
        <input placeholder="Employee code" />
        <input placeholder="Email" />
      </Modal>,
    )

    expect(screen.getByPlaceholderText('Employee code')).toHaveFocus()
  })

  it('does not steal focus back to the first field when the parent re-renders with a new onClose', async () => {
    // Mirrors every real caller (ManagerPage.jsx): onClose={() => setModalOpen(null)} is a fresh
    // inline function every render, and typing into the modal's own form is exactly what causes
    // that parent re-render - this is the real bug: focus was jumping back to the first field
    // on every keystroke.
    function Harness() {
      const [, forceRerender] = useState(0)
      return (
        <Modal title="Add Employee" onClose={() => forceRerender((n) => n + 1)}>
          <input placeholder="Employee code" />
          <input placeholder="Email" onChange={() => forceRerender((n) => n + 1)} />
        </Modal>
      )
    }

    const user = userEvent.setup()
    render(<Harness />)

    const emailInput = screen.getByPlaceholderText('Email')
    await user.click(emailInput)
    await user.type(emailInput, 'a@b.com')

    expect(emailInput).toHaveFocus()
    expect(emailInput).toHaveValue('a@b.com')
  })

  it('calls onClose on Escape', async () => {
    const onClose = vi.fn()
    const user = userEvent.setup()
    render(
      <Modal title="Add Employee" onClose={onClose}>
        <input placeholder="Employee code" />
      </Modal>,
    )

    await user.keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledTimes(1)
  })
})
