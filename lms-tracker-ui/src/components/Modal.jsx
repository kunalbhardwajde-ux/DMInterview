import React, { useEffect, useId, useRef } from 'react'

const FOCUSABLE_SELECTOR = 'button, input, select, textarea, a[href]'

export function Modal({ title, onClose, children }) {
  const titleId = useId()
  const dialogRef = useRef(null)

  // Focus the first focusable element once, when the modal opens. Deliberately its own effect
  // with an empty dependency array - it only touches dialogRef (a ref, exempt from
  // exhaustive-deps) and nothing reactive, so it never needs to re-run. It must NOT share the
  // effect below: onClose is commonly passed as a fresh inline arrow function by callers, so if
  // this ran on every onClose identity change, it would steal focus back to the first field on
  // every re-render of the modal's own form - i.e. on every keystroke.
  useEffect(() => {
    const focusable = dialogRef.current?.querySelector(FOCUSABLE_SELECTOR)
    focusable?.focus()
  }, [])

  useEffect(() => {
    const dialog = dialogRef.current

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
