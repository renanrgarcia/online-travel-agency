import type { Strings } from '../i18n/strings'

/**
 * Display-only formatting for a {@link RankedOffer} — mirrors the exact conventions
 * `PriceReferenceStore` uses server-side (symbol per currency, `{h}h {m}m`, `nonstop`/`N stop(s)`),
 * so a traveller sees the same shapes here as in the explanation prose.
 *
 * Formatting is not the same thing as altering a value (F04 E2): none of these round, convert, or
 * otherwise change the number the payload sent — they only choose how to print it.
 */

const CURRENCY_SYMBOLS: Record<string, string> = { USD: '$', BRL: 'R$', EUR: '€' }

export function formatPrice(price: number, currency: string): string {
  const symbol = CURRENCY_SYMBOLS[currency]
  return symbol ? `${symbol}${price.toFixed(2)}` : `${price.toFixed(2)} ${currency}`
}

export function formatDuration(durationMinutes: number): string {
  const hours = Math.floor(durationMinutes / 60)
  const minutes = durationMinutes % 60
  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`
}

export function formatStops(stops: number, strings: Strings): string {
  if (stops === 0) return strings.stopsNonstop
  return stops === 1 ? strings.stopsOne : strings.stopsMany.replace('{n}', String(stops))
}
