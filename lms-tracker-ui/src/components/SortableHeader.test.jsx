import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { SortableHeader } from './SortableHeader'

describe('SortableHeader', () => {
  it('calls onSort with its own key when clicked', async () => {
    const onSort = vi.fn()
    const user = userEvent.setup()
    render(
      <table>
        <thead>
          <tr>
            <SortableHeader label="Name" sortKey="name" currentSortKey="" currentSortDirection="asc" onSort={onSort} />
          </tr>
        </thead>
      </table>,
    )

    await user.click(screen.getByRole('button', { name: /Name/ }))

    expect(onSort).toHaveBeenCalledWith('name')
  })

  it('reflects the active sort direction via aria-sort', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableHeader label="Name" sortKey="name" currentSortKey="name" currentSortDirection="desc" onSort={() => {}} />
          </tr>
        </thead>
      </table>,
    )

    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'descending')
  })

  it('reports aria-sort="none" when this column is not the active one', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableHeader label="Name" sortKey="name" currentSortKey="score" currentSortDirection="asc" onSort={() => {}} />
          </tr>
        </thead>
      </table>,
    )

    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'none')
  })
})
