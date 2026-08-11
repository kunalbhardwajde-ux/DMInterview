import React, { useEffect, useId, useRef } from 'react'

const FOCUSABLE_SELECTOR = 'button, input, select, textarea, a[href]'

export function Modal({ title, onClose, children }) {
  const titleId = useId()
  const dialogRef = useRef(null)

  useEffect(() => {
    const dialog = dialogRef.current
    const focusable = dialog?.querySelector(FOCUSABLE_SELECTOR)
    focusable?.focus()

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        onClose()
        return
      }

      if (event.key !== 'Tab' || !dialog) {
        return
      }

      const focusableElements = dialog.querySelectorAll(FOCUSABLE_SELECTOR)
      if (focusableElements.length === 0) {
        return
      }

      const first = focusableElements[0]
      const last = focusableElements[focusableElements.length - 1]

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

  return (
    <section className="modal" role="dialog" aria-modal="true" aria-labelledby={titleId} ref={dialogRef}>
      <h2 id={titleId}>{title}</h2>
      {children}
    </section>
  )
}
