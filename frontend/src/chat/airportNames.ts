import type { Language } from '../i18n/strings'

/**
 * Display names for this demo's known corridors — not a general IATA database. A real supplier or the
 * intent agent can return any code; anything not listed here just falls back to the bare code (below),
 * the same thing the UI showed before this map existed, never a blank or a crash.
 */
const AIRPORT_NAMES: Record<string, Record<Language, string>> = {
  GRU: { en: 'São Paulo–Guarulhos', 'pt-BR': 'São Paulo–Guarulhos' },
  CGH: { en: 'São Paulo–Congonhas', 'pt-BR': 'São Paulo–Congonhas' },
  VCP: { en: 'Campinas–Viracopos', 'pt-BR': 'Campinas–Viracopos' },
  SAO: { en: 'São Paulo', 'pt-BR': 'São Paulo' },
  GIG: { en: 'Rio de Janeiro–Galeão', 'pt-BR': 'Rio de Janeiro–Galeão' },
  SDU: { en: 'Rio de Janeiro–Santos Dumont', 'pt-BR': 'Rio de Janeiro–Santos Dumont' },
  RIO: { en: 'Rio de Janeiro', 'pt-BR': 'Rio de Janeiro' },
  LIS: { en: 'Lisbon', 'pt-BR': 'Lisboa' },
  BRC: { en: 'Bariloche', 'pt-BR': 'Bariloche' },
}

export function airportDisplayName(code: string | null | undefined, language: Language): string {
  if (!code) return ''
  return AIRPORT_NAMES[code]?.[language] ?? code
}
