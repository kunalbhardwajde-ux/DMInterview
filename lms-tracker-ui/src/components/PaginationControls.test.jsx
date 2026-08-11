import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { PaginationControls } from './PaginationControls'

describe('PaginationControls', () => {
  it('renders nothing when there is one page or fewer', () => {
    const { container } = render(<PaginationControls page={1} totalPages={1} onPageChange={() => {}} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('disables Prev on the first page and Next on the last page', () => {
    render(<PaginationControls page={1} totalPages={3} onPageChange={() => {}} />)

    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).not.toBeDisabled()
  })

  it('calls onPageChange with the target page when a page number is clicked', async () => {
    const user = userEvent.setup()
    const onPageChange = vi.fn()
    render(<PaginationControls page={1} totalPages={3} onPageChange={onPageChange} />)

    await user.click(screen.getByRole('button', { name: '2' }))

    expect(onPageChange).toHaveBeenCalledWith(2)
  })

  it('calls onPageChange with page + 1 when Next is clicked', async () => {
    const user = userEvent.setup()
    const onPageChange = vi.fn()
    render(<PaginationControls page={2} totalPages={3} onPageChange={onPageChange} />)

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(onPageChange).toHaveBeenCalledWith(3)
  })
})
