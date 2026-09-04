import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'

import { STRINGS, type Language, type Strings } from './strings'

interface LanguageContextValue {
  language: Language
  strings: Strings
  setLanguage: (language: Language) => void
}

const LanguageContext = createContext<LanguageContextValue | undefined>(undefined)

/**
 * The manual toggle (F01) plus the automatic sync `App` drives once a search resolves a language
 * (F07's locked decision: `parsed-intent.language` over browser locale). Both write the same piece of
 * state — there's no separate "auto" vs. "manual" mode to reconcile, just whichever set it last.
 */
export function LanguageProvider({
  children,
  initialLanguage = 'en',
}: {
  children: ReactNode
  initialLanguage?: Language
}) {
  const [language, setLanguage] = useState<Language>(initialLanguage)
  const value = useMemo(() => ({ language, strings: STRINGS[language], setLanguage }), [language])

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}

export function useLanguage(): LanguageContextValue {
  const context = useContext(LanguageContext)
  if (!context) throw new Error('useLanguage must be used within a LanguageProvider')
  return context
}

/**
 * Freezes `useLanguage()` to a fixed language for its subtree, overriding whatever the ambient
 * chrome language is at render time. A turn's content must keep the language it was answered in even
 * after a later, differently-languaged search changes the app's chrome (F07 E3) — this is how that
 * subtree opts out of the ambient value without every leaf component needing its own language prop.
 * `setLanguage` is a no-op here: nothing inside a frozen turn should be changing chrome-wide state.
 */
export function LanguageOverride({ language, children }: { language: Language; children: ReactNode }) {
  const value = useMemo<LanguageContextValue>(
    () => ({ language, strings: STRINGS[language], setLanguage: () => {} }),
    [language],
  )
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}
