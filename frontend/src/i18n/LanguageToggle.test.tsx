import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { LanguageProvider, useLanguage } from './LanguageProvider'
import { LanguageToggle } from './LanguageToggle'
import { STRINGS } from './strings'

/** Renders whatever chrome string is currently selected, so a test can observe the switch happen. */
function ObservedString({ pick }: { pick: (s: typeof STRINGS.en) => string }) {
  const { strings } = useLanguage()
  return <p>{pick(strings)}</p>
}

describe('LanguageToggle', () => {
  it('defaults to English', () => {
    render(
      <LanguageProvider>
        <LanguageToggle />
        <ObservedString pick={(s) => s.composerLabel} />
      </LanguageProvider>,
    )

    expect(screen.getByText(STRINGS.en.composerLabel)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'EN' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'PT-BR' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('switches every wired string when Portuguese is picked', async () => {
    const user = userEvent.setup()
    render(
      <LanguageProvider>
        <LanguageToggle />
        <ObservedString pick={(s) => s.composerLabel} />
      </LanguageProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'PT-BR' }))

    expect(screen.getByText(STRINGS['pt-BR'].composerLabel)).toBeInTheDocument()
    expect(screen.queryByText(STRINGS.en.composerLabel)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'PT-BR' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'EN' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('switches back to English', async () => {
    const user = userEvent.setup()
    render(
      <LanguageProvider initialLanguage="pt-BR">
        <LanguageToggle />
        <ObservedString pick={(s) => s.composerLabel} />
      </LanguageProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'EN' }))

    expect(screen.getByText(STRINGS.en.composerLabel)).toBeInTheDocument()
  })

  it('is reachable and operable by keyboard', async () => {
    const user = userEvent.setup()
    render(
      <LanguageProvider>
        <LanguageToggle />
      </LanguageProvider>,
    )

    await user.tab()
    expect(screen.getByRole('button', { name: 'EN' })).toHaveFocus()

    await user.keyboard('{Enter}')
    // Already English; pressing it again should not throw or change selection unexpectedly.
    expect(screen.getByRole('button', { name: 'EN' })).toHaveAttribute('aria-pressed', 'true')

    await user.tab()
    await user.keyboard('{Enter}')
    expect(screen.getByRole('button', { name: 'PT-BR' })).toHaveAttribute('aria-pressed', 'true')
  })
})

describe('useLanguage', () => {
  it('throws when used outside a LanguageProvider', () => {
    // Suppress the expected React error-boundary console noise for this one assertion.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    function Unwrapped() {
      useLanguage()
      return null
    }

    expect(() => render(<Unwrapped />)).toThrow('useLanguage must be used within a LanguageProvider')

    consoleError.mockRestore()
  })
})
