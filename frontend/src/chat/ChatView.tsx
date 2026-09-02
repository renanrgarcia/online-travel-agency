import { useEffect, useRef } from 'react'

import { AssistantTurnView } from './AssistantTurnView'
import { BookingTurnView } from './BookingTurnView'
import { Composer } from './Composer'
import { EmptyState } from './EmptyState'
import { isPinnedToBottom } from './autoScroll'
import type { RankedOffer } from '../api/contract'
import type { Turn } from './types'
import { useLanguage } from '../i18n/LanguageProvider'

export interface ChatViewProps {
  turns: Turn[]
  isStreaming: boolean
  onSubmit: (text: string) => void
  onBookOffer: (offer: RankedOffer) => void
  onConfirmBooking: (turnId: string, bookingId: string, offer: RankedOffer, travellerEmail: string) => void
  onCancelBooking: (turnId: string) => void
}

export function ChatView({
  turns,
  isStreaming,
  onSubmit,
  onBookOffer,
  onConfirmBooking,
  onCancelBooking,
}: ChatViewProps) {
  const { strings } = useLanguage()
  const listRef = useRef<HTMLDivElement>(null)
  const pinnedToBottom = useRef(true)

  // Remember whether the user is following along *before* new content changes the geometry.
  const handleScroll = () => {
    const element = listRef.current
    if (element) pinnedToBottom.current = isPinnedToBottom(element)
  }

  useEffect(() => {
    const element = listRef.current
    // Only follow the newest content if the user hasn't deliberately scrolled back up.
    if (element && pinnedToBottom.current) element.scrollTop = element.scrollHeight
  }, [turns])

  return (
    <div className="chat">
      <div className="chat__log" ref={listRef} onScroll={handleScroll} data-testid="chat-log">
        {turns.length === 0 ? (
          <EmptyState onPickSuggestion={onSubmit} />
        ) : (
          turns.map((turn) => {
            if (turn.role === 'user') {
              return (
                <article key={turn.id} className="turn turn--user" aria-label={strings.youAskedLabel}>
                  <p>{turn.text}</p>
                </article>
              )
            }
            if (turn.role === 'booking') {
              return (
                <BookingTurnView
                  key={turn.id}
                  turn={turn}
                  onConfirm={onConfirmBooking}
                  onCancel={onCancelBooking}
                />
              )
            }
            return <AssistantTurnView key={turn.id} turn={turn} onBookOffer={onBookOffer} />
          })
        )}
      </div>

      <Composer onSubmit={onSubmit} disabled={isStreaming} />
    </div>
  )
}
