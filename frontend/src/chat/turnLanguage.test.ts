import { describe, expect, it } from 'vitest'

import { assistantTurnLanguage, latestResolvedTurnLanguage } from './turnLanguage'
import { emptyStages, type AssistantTurn, type Turn, type UserTurn } from './types'

function assistantTurn(overrides: Partial<AssistantTurn> = {}): AssistantTurn {
  return { id: 'assistant-0', role: 'assistant', status: 'streaming', stages: emptyStages(), ...overrides }
}

function parsedIntent(language: string) {
  return { origin: 'GRU', destination: 'LIS', departureDate: '2027-03-12', passengerCount: 1, language }
}

describe('assistantTurnLanguage', () => {
  it('falls back to the given default before parsed-intent has arrived', () => {
    expect(assistantTurnLanguage(assistantTurn(), 'pt-BR')).toBe('pt-BR')
  })

  it('maps a pt-BR parsed-intent language onto the UI language, ignoring the fallback', () => {
    const turn = assistantTurn({ stages: { ...emptyStages(), parsedIntent: parsedIntent('pt-BR') } })
    expect(assistantTurnLanguage(turn, 'en')).toBe('pt-BR')
  })

  it('maps an english parsed-intent language onto the UI language', () => {
    const turn = assistantTurn({ stages: { ...emptyStages(), parsedIntent: parsedIntent('en') } })
    expect(assistantTurnLanguage(turn, 'pt-BR')).toBe('en')
  })

  it('falls back to English for a language outside pt-BR/en, per F07 scope', () => {
    const turn = assistantTurn({ stages: { ...emptyStages(), parsedIntent: parsedIntent('fr') } })
    expect(assistantTurnLanguage(turn, 'pt-BR')).toBe('en')
  })
})

describe('latestResolvedTurnLanguage', () => {
  it('returns undefined with no turns at all', () => {
    expect(latestResolvedTurnLanguage([])).toBeUndefined()
  })

  it('returns undefined while the only turn has no parsed-intent yet', () => {
    const turns: Turn[] = [assistantTurn()]
    expect(latestResolvedTurnLanguage(turns)).toBeUndefined()
  })

  it('returns the resolved language of the most recent assistant turn', () => {
    const turns: Turn[] = [
      assistantTurn({ id: 'a1', stages: { ...emptyStages(), parsedIntent: parsedIntent('en') } }),
      { id: 'u2', role: 'user', text: 'segunda busca' } satisfies UserTurn,
      assistantTurn({ id: 'a2', stages: { ...emptyStages(), parsedIntent: parsedIntent('pt-BR') } }),
    ]
    expect(latestResolvedTurnLanguage(turns)).toBe('pt-BR')
  })

  it('skips a trailing turn that has not resolved a language yet, using the last one that did', () => {
    const turns: Turn[] = [
      assistantTurn({ id: 'a1', stages: { ...emptyStages(), parsedIntent: parsedIntent('pt-BR') } }),
      assistantTurn({ id: 'a2' }), // still streaming, no parsed-intent yet
    ]
    expect(latestResolvedTurnLanguage(turns)).toBe('pt-BR')
  })
})
