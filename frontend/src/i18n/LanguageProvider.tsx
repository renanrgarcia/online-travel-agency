import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'

import { STRINGS, type Language, type Strings } from './strings'

interface LanguageContextValue {
  language: Language
  strings: Strings
  setLanguage: (language: Language) => void
}

const LanguageContext = createContext<LanguageContextValue | undefined>(undefined)

/**
 * The manual override from F07's locked decisions, scoped to today's chrome only. Once F03 wires the
 * real stream in, `parsed-intent.language` becomes the source of truth for a completed search — this
 * provider doesn't attempt that yet, since there's no stream to read it from.
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
