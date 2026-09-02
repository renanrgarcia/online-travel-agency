import { toUiLanguage, type Language } from '../i18n/strings'
import type { AssistantTurn, Turn } from './types'

/**
 * The language this specific turn was (or will be) answered in. `undefined` until its own
 * `parsed-intent` resolves, at which point it freezes for the lifetime of the turn (F07 E3) — later
 * turns changing language never retroactively relabel this one. Falls back to `fallback` (the ambient
 * chrome language) while still unknown, e.g. during "Understanding your search…".
 */
export function assistantTurnLanguage(turn: AssistantTurn, fallback: Language): Language {
  return turn.stages.parsedIntent ? toUiLanguage(turn.stages.parsedIntent.language) : fallback
}

/**
 * The most recently resolved turn language in the conversation, if any. Drives the app's chrome
 * default once at least one search has told us what language its user actually typed in — better
 * evidence than a system setting (F07's locked decision).
 */
export function latestResolvedTurnLanguage(turns: readonly Turn[]): Language | undefined {
  for (let i = turns.length - 1; i >= 0; i--) {
    const turn = turns[i]
    if (turn?.role === 'assistant' && turn.stages.parsedIntent) {
      return toUiLanguage(turn.stages.parsedIntent.language)
    }
  }
  return undefined
}
