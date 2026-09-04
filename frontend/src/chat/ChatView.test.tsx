import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { ChatView, type ChatViewProps } from './ChatView'
import { isPinnedToBottom } from './autoScroll'
import { useChat } from './useChat'
import { emptyStages, type AssistantTurn, type Turn } from './types'
import { LanguageProvider } from '../i18n/LanguageProvider'
import { STRINGS } from '../i18n/strings'

/** One test per eval in docs/features/02-frontend/tasks/02-chat-shell.md. Asserted against the
 * English strings — LanguageToggle.test.tsx covers the toggle itself switching them. */
const strings = STRINGS.en

/** `ChatView` with the booking wiring defaulted to no-ops — F02's own evals don't exercise booking
 * (that's F05's `useBookingFlow.test.ts`); this keeps every call site here focused on chat/search. */
function TestChatView(props: Pick<ChatViewProps, 'turns' | 'isStreaming' | 'onSubmit'>) {
  return (
    <ChatView
      {...props}
      onBookOffer={() => {}}
      onConfirmBooking={() => {}}
      onCancelBooking={() => {}}
      onResetConversation={() => {}}
    />
  )
}

/** A real `useChat` wired to `ChatView`, plus buttons a test can click to drive turn state. */
function Harness() {
  const chat = useChat()
  const streamingTurn = chat.turns.find(
    (turn): turn is AssistantTurn => turn.role === 'assistant' && turn.status === 'streaming',
  )

  return (
    <>
      <TestChatView turns={chat.turns} isStreaming={chat.isStreaming} onSubmit={chat.submit} />
      <button
        type="button"
        onClick={() => streamingTurn && chat.completeTurn(streamingTurn.id)}
      >
        finish stream
      </button>
    </>
  )
}

function renderWithinProvider(ui: React.ReactElement) {
  return render(<LanguageProvider>{ui}</LanguageProvider>)
}

function assistantTurn(stages: Partial<AssistantTurn['stages']> = {}): AssistantTurn {
  return {
    id: 'assistant-0',
    role: 'assistant',
    status: 'streaming',
    stages: { ...emptyStages(), ...stages },
  }
}

describe('ChatView', () => {
  it('E1 — a submitted message creates a user turn and a pending assistant turn', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    await user.type(screen.getByRole('textbox', { name: /search for a flight/i }), 'lisbon please')
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))

    expect(screen.getByLabelText(strings.youAskedLabel)).toHaveTextContent('lisbon please')
    expect(screen.getByLabelText(strings.resultsLabel)).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(strings.searching)
  })

  it('E2 — renders the stages that arrived and nothing at all for the ones that have not', () => {
    const turns: Turn[] = [
      { id: 'user-0', role: 'user', text: 'lisbon' },
      assistantTurn({
        parsedIntent: {
          origin: 'GRU',
          destination: 'LIS',
          departureDate: '2027-03-12',
          passengerCount: 2,
          language: 'en',
        },
        supplierResults: [
          { supplierName: 'GDS', status: 'Succeeded', offerCount: 2, reason: null },
        ],
      }),
    ]

    renderWithinProvider(<TestChatView turns={turns} isStreaming onSubmit={() => {}} />)

    // The two stages that arrived.
    expect(screen.getByRole('heading', { name: strings.stageUnderstood })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: strings.stageSuppliers })).toBeInTheDocument()
    expect(screen.getByText('GRU → LIS · 2027-03-12 · 2 travellers')).toBeInTheDocument()

    // The two that have not: absent entirely, not empty frames.
    expect(screen.queryByRole('heading', { name: strings.stageOffers })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: strings.stageWhy })).not.toBeInTheDocument()
  })

  it('E2 — a turn with no stages yet renders only its pending state', () => {
    renderWithinProvider(<TestChatView turns={[assistantTurn()]} isStreaming onSubmit={() => {}} />)

    expect(screen.getByRole('status')).toHaveTextContent(strings.searching)
    expect(screen.queryByRole('heading', { name: strings.stageUnderstood })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: strings.stageSuppliers })).not.toBeInTheDocument()
  })

  it('E3 — the composer is disabled while a search is in flight, with a visible reason', () => {
    renderWithinProvider(<TestChatView turns={[assistantTurn()]} isStreaming onSubmit={() => {}} />)

    expect(screen.getByRole('textbox', { name: /search for a flight/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: strings.composerSubmit })).toBeDisabled()
    expect(screen.getByText(strings.composerStreamingHint)).toBeInTheDocument()
  })

  it('E4 — the composer re-enables and takes focus once the turn completes', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    const input = screen.getByRole('textbox', { name: /search for a flight/i })
    await user.type(input, 'lisbon')
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))
    expect(input).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'finish stream' }))

    expect(input).toBeEnabled()
    expect(input).toHaveFocus()
    expect(screen.queryByText(strings.composerStreamingHint)).not.toBeInTheDocument()
  })

  it('E5 — a whitespace-only submission is rejected inline and creates no turn', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    await user.type(screen.getByRole('textbox', { name: /search for a flight/i }), '    ')
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))

    expect(screen.getByRole('alert')).toHaveTextContent(strings.composerEmptyError)
    expect(screen.queryByLabelText(strings.youAskedLabel)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(strings.resultsLabel)).not.toBeInTheDocument()
  })

  it('E5 — the inline error clears as soon as the user edits the field', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    const input = screen.getByRole('textbox', { name: /search for a flight/i })
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))
    expect(screen.getByRole('alert')).toBeInTheDocument()

    await user.type(input, 'l')

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('E6 — follows the newest content while the user is pinned to the bottom', () => {
    const { rerender } = renderWithinProvider(
      <TestChatView turns={[]} isStreaming={false} onSubmit={() => {}} />,
    )
    const log = screen.getByTestId('chat-log')
    Object.defineProperty(log, 'scrollHeight', { value: 1000, configurable: true })
    Object.defineProperty(log, 'clientHeight', { value: 400, configurable: true })

    rerender(
      <LanguageProvider>
        <TestChatView
          turns={[{ id: 'user-0', role: 'user', text: 'lisbon' }]}
          isStreaming={false}
          onSubmit={() => {}}
        />
      </LanguageProvider>,
    )

    expect(log.scrollTop).toBe(1000)
  })

  it('E6 — does not fight a user who has scrolled back up', () => {
    const { rerender } = renderWithinProvider(
      <TestChatView turns={[]} isStreaming={false} onSubmit={() => {}} />,
    )
    const log = screen.getByTestId('chat-log')
    Object.defineProperty(log, 'scrollHeight', { value: 1000, configurable: true })
    Object.defineProperty(log, 'clientHeight', { value: 400, configurable: true })

    // The user scrolls well away from the bottom, and the component records it.
    log.scrollTop = 100
    log.dispatchEvent(new Event('scroll', { bubbles: true }))

    rerender(
      <LanguageProvider>
        <TestChatView
          turns={[{ id: 'user-0', role: 'user', text: 'lisbon' }]}
          isStreaming={false}
          onSubmit={() => {}}
        />
      </LanguageProvider>,
    )

    expect(log.scrollTop).toBe(100)
  })

  it('E7 — a search can be composed and submitted with the keyboard alone', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    // Tab order: the empty state's suggestion comes first, then the composer.
    await user.tab()
    expect(screen.getByRole('button', { name: strings.emptyStateSuggestion })).toHaveFocus()

    await user.tab()
    expect(screen.getByRole('textbox', { name: /search for a flight/i })).toHaveFocus()

    await user.keyboard('lisbon{Enter}')

    expect(screen.getByLabelText(strings.youAskedLabel)).toHaveTextContent('lisbon')
  })

  it('E7 — assistant turns are announced as their stages arrive', () => {
    const turn = assistantTurn({
      explanation: { text: 'The best value is $590.00.', raw: '', isClean: true },
    })
    renderWithinProvider(<TestChatView turns={[turn]} isStreaming onSubmit={() => {}} />)

    const liveRegion = screen.getByLabelText(strings.resultsLabel).querySelector('[aria-live]')
    expect(liveRegion).not.toBeNull()
    expect(liveRegion).toHaveAttribute('aria-live', 'polite')
    expect(within(liveRegion as HTMLElement).getByText('The best value is $590.00.')).toBeInTheDocument()
  })

  it('E8 — the empty state offers a suggestion that runs a real search', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    const suggestion = screen.getByRole('button', { name: strings.emptyStateSuggestion })
    await user.click(suggestion)

    expect(screen.getByLabelText(strings.youAskedLabel)).toHaveTextContent(strings.emptyStateSuggestion)
    expect(
      screen.queryByRole('button', { name: strings.emptyStateSuggestion }),
    ).not.toBeInTheDocument()
  })

  it('E8 — the empty state disappears once a conversation exists', () => {
    renderWithinProvider(
      <TestChatView
        turns={[{ id: 'user-0', role: 'user', text: 'lisbon' }]}
        isStreaming={false}
        onSubmit={() => {}}
      />,
    )

    expect(screen.queryByRole('button', { name: strings.emptyStateSuggestion })).not.toBeInTheDocument()
  })

  it('keeps earlier turns intact when a second search runs', async () => {
    const user = userEvent.setup()
    renderWithinProvider(<Harness />)

    await user.type(screen.getByRole('textbox', { name: /search for a flight/i }), 'first')
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))
    await user.click(screen.getByRole('button', { name: 'finish stream' }))

    await user.type(screen.getByRole('textbox', { name: /search for a flight/i }), 'second')
    await user.click(screen.getByRole('button', { name: strings.composerSubmit }))

    const userTurns = screen.getAllByLabelText(strings.youAskedLabel)
    expect(userTurns).toHaveLength(2)
    expect(userTurns[0]).toHaveTextContent('first')
    expect(userTurns[1]).toHaveTextContent('second')
  })
})

describe('isPinnedToBottom', () => {
  it('is true at the bottom and within the follow threshold', () => {
    expect(isPinnedToBottom({ scrollTop: 600, scrollHeight: 1000, clientHeight: 400 })).toBe(true)
    expect(isPinnedToBottom({ scrollTop: 560, scrollHeight: 1000, clientHeight: 400 })).toBe(true)
  })

  it('is false once the user has scrolled meaningfully away', () => {
    expect(isPinnedToBottom({ scrollTop: 200, scrollHeight: 1000, clientHeight: 400 })).toBe(false)
  })
})
