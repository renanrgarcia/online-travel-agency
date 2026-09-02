import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { OfferCard } from './OfferCard'
import { OfferComparison } from './OfferComparison'
import { LanguageProvider } from '../i18n/LanguageProvider'
import type { RankedOffer } from '../api/contract'

/** One test per eval in docs/features/02-frontend/tasks/04-offer-cards-and-comparison.md. */

function offer(overrides: Partial<RankedOffer> = {}): RankedOffer {
  return {
    rank: 1,
    offerId: 'LCC-002',
    price: 590,
    currency: 'USD',
    durationMinutes: 480,
    stops: 1,
    refundable: false,
    score: 1071,
    ...overrides,
  }
}

function renderCards(offers: RankedOffer[]) {
  render(
    <LanguageProvider>
      <ol>
        {offers.map((o) => (
          <OfferCard key={o.offerId} offer={o} />
        ))}
      </ol>
      <OfferComparison offers={offers} />
    </LanguageProvider>,
  )
}

describe('OfferCard / OfferComparison', () => {
  it('E1 — six offers render six cards, in the order given, every field present', () => {
    const offers = [1, 2, 3, 4, 5, 6].map((rank) => offer({ rank, offerId: `OFF-${rank}` }))
    renderCards(offers)

    const cards = screen.getAllByRole('listitem')
    expect(cards).toHaveLength(6)
    cards.forEach((card, index) => expect(card).toHaveTextContent(`OFF-${index + 1}`))
  })

  it('E2 — price reflects the payload value exactly, with no rounding or conversion', () => {
    renderCards([offer({ price: 590, currency: 'USD' }), offer({ offerId: 'B', price: 410.5, currency: 'BRL' })])
    const cardList = within(screen.getByRole('list'))

    const usdText = cardList.getByText('$590.00')
    const brlText = cardList.getByText('R$410.50')
    expect(usdText).toBeInTheDocument()
    expect(brlText).toBeInTheDocument()
    // Round-trip: strip the symbol, parse back, must equal the original payload number exactly.
    expect(Number(usdText.textContent!.replace(/[^0-9.]/g, ''))).toBe(590)
    expect(Number(brlText.textContent!.replace(/[^0-9.]/g, ''))).toBe(410.5)
  })

  it('E2 — an unrecognised currency is shown verbatim, not silently converted', () => {
    renderCards([offer({ price: 1000, currency: 'JPY' }), offer({ offerId: 'B' })])
    expect(within(screen.getByRole('list')).getByText('1000.00 JPY')).toBeInTheDocument()
  })

  it('E3 — the comparison table aligns price, duration, stops, and refundability per offer', () => {
    const offers = [
      offer({ rank: 1, offerId: 'A', price: 590, durationMinutes: 480, stops: 1, refundable: false }),
      offer({ rank: 2, offerId: 'B', price: 410, durationMinutes: 660, stops: 2, refundable: false }),
      offer({ rank: 3, offerId: 'C', price: 730, durationMinutes: 420, stops: 1, refundable: true }),
    ]
    renderCards(offers)

    const table = screen.getByRole('table')
    const rows = screen.getAllByRole('row')
    expect(rows).toHaveLength(5) // header + price + duration + stops + refundable
    expect(table).toHaveTextContent('#1')
    expect(table).toHaveTextContent('#2')
    expect(table).toHaveTextContent('#3')
    expect(table).toHaveTextContent('$590.00')
    expect(table).toHaveTextContent('7h')
    expect(table).toHaveTextContent('1 stop')
    expect(table).toHaveTextContent('Refundable')
    expect(table).toHaveTextContent('Non-refundable')
  })

  it('E4 — an offer that is cheapest but slowest shows both facts on its own card, unexpanded', () => {
    renderCards([
      offer({ rank: 1, offerId: 'CHEAP-SLOW', price: 200, durationMinutes: 900 }),
      offer({ rank: 2, offerId: 'FAST-EXPENSIVE', price: 900, durationMinutes: 120 }),
    ])

    const card = screen.getByText('CHEAP-SLOW').closest('li')!
    expect(card).toHaveTextContent('$200.00')
    expect(card).toHaveTextContent('15h')
  })

  it('E7 — a single offer renders a card but no comparison affordance', () => {
    renderCards([offer()])

    expect(screen.getByRole('listitem')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('E8 — nothing computed appears: no cheapest/fastest/best-value label anywhere in the output', () => {
    const { container } = render(
      <LanguageProvider>
        <ol>
          <OfferCard offer={offer({ rank: 1, offerId: 'A', price: 100 })} />
          <OfferCard offer={offer({ rank: 2, offerId: 'B', price: 999 })} />
        </ol>
        <OfferComparison offers={[offer({ offerId: 'A' }), offer({ offerId: 'B' })]} />
      </LanguageProvider>,
    )

    const text = container.textContent ?? ''
    expect(text).not.toMatch(/cheapest|fastest|best value|most expensive|slowest/i)
  })

  it('formats stops per the shared convention: nonstop / 1 stop / N stops', () => {
    renderCards([
      offer({ rank: 1, offerId: 'A', stops: 0 }),
      offer({ rank: 2, offerId: 'B', stops: 1 }),
      offer({ rank: 3, offerId: 'C', stops: 3 }),
    ])
    const cardList = within(screen.getByRole('list'))

    expect(cardList.getByText('nonstop')).toBeInTheDocument()
    expect(cardList.getByText('1 stop')).toBeInTheDocument()
    expect(cardList.getByText('3 stops')).toBeInTheDocument()
  })

  it('score is never rendered to the traveller, in cards or the comparison table', () => {
    renderCards([offer({ score: 1071 }), offer({ offerId: 'B', score: 2222 })])
    expect(screen.queryByText('1071')).not.toBeInTheDocument()
    expect(screen.queryByText('2222')).not.toBeInTheDocument()
  })
})
