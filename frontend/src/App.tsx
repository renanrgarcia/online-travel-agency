import { useEffect } from 'react'

import { ChatView } from './chat/ChatView'
import { latestResolvedTurnLanguage } from './chat/turnLanguage'
import { useBookingFlow } from './chat/useBookingFlow'
import { useSearchChat } from './chat/useSearchChat'
import { LanguageProvider } from './i18n/LanguageProvider'
import { LanguageToggle } from './i18n/LanguageToggle'
import { useLanguage } from './i18n/LanguageProvider'

function AppShell() {
  const { strings, setLanguage } = useLanguage()
  const chat = useSearchChat()
  const booking = useBookingFlow(chat)

  // Once a search tells us what language its own query was actually in, that's better evidence than
  // whatever the chrome was defaulting to -- browser locale or an earlier manual pick (F07's locked
  // decision). Each new resolved search can move chrome again; already-rendered turns don't, since
  // they read their own frozen language rather than this ambient one.
  const latestLanguage = latestResolvedTurnLanguage(chat.turns)
  useEffect(() => {
    if (latestLanguage) setLanguage(latestLanguage)
  }, [latestLanguage, setLanguage])

  return (
    <main className="app">
      <header className="app__header">
        <h1 className="app__title">{strings.appTitle}</h1>
        <LanguageToggle />
      </header>
      <ChatView
        turns={chat.turns}
        isStreaming={chat.isStreaming}
        onSubmit={chat.submit}
        onBookOffer={booking.startBooking}
        onConfirmBooking={booking.confirmBooking}
        onCancelBooking={chat.removeTurn}
      />
    </main>
  )
}

export function App() {
  return (
    <LanguageProvider>
      <AppShell />
    </LanguageProvider>
  )
}
