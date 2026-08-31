import { useEffect, useRef, useState, type FormEvent } from 'react'

import { useLanguage } from '../i18n/LanguageProvider'

export interface ComposerProps {
  onSubmit: (text: string) => void
  /** One in-flight search at a time, so the composer closes while one is running. */
  disabled: boolean
}

export function Composer({ onSubmit, disabled }: ComposerProps) {
  const { strings } = useLanguage()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const wasDisabled = useRef(disabled)

  // Return focus once the search finishes, so the next query needs no click.
  useEffect(() => {
    if (wasDisabled.current && !disabled) inputRef.current?.focus()
    wasDisabled.current = disabled
  }, [disabled])

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (disabled) return

    if (text.trim().length === 0) {
      // Caught here rather than round-tripped: the intent agent rejects a blank message outright.
      setError(strings.composerEmptyError)
      return
    }

    setError(null)
    onSubmit(text)
    setText('')
  }

  return (
    <form className="composer" onSubmit={handleSubmit}>
      <label className="composer__label" htmlFor="composer-input">
        {strings.composerLabel}
      </label>
      <div className="composer__row">
        <input
          id="composer-input"
          ref={inputRef}
          className="composer__input"
          type="text"
          autoComplete="off"
          value={text}
          disabled={disabled}
          aria-invalid={error !== null}
          aria-describedby={error ? 'composer-error' : disabled ? 'composer-disabled' : undefined}
          placeholder={strings.composerPlaceholder}
          onChange={(event) => {
            setText(event.target.value)
            if (error) setError(null)
          }}
        />
        <button className="composer__submit" type="submit" disabled={disabled}>
          {strings.composerSubmit}
        </button>
      </div>
      {error && (
        <p className="composer__error" id="composer-error" role="alert">
          {error}
        </p>
      )}
      {disabled && (
        <p className="composer__hint" id="composer-disabled">
          {strings.composerStreamingHint}
        </p>
      )}
    </form>
  )
}
