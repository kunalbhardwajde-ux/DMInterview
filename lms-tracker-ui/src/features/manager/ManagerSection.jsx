import React from 'react'

export function ManagerSection({ title, description, actions, children }) {
  return (
    <section className="panel">
      <div className="panel-title-row">
        <div>
          <h2>{title}</h2>
          {description ? <p className="muted-inline">{description}</p> : null}
        </div>
        {actions ? <div className="quick-actions">{actions}</div> : null}
      </div>
      {children}
    </section>
  )
}
