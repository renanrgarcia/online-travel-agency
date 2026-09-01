import { ChatView } from './chat/ChatView'
import { useChat } from './chat/useChat'
import { LanguageProvider } from './i18n/LanguageProvider'
import { LanguageToggle } from './i18n/LanguageToggle'
import { useLanguage } from './i18n/LanguageProvider'

/**
 * Task F02 is the shell only — nothing opens the search stream yet, so a submitted turn stays in its
 * pending state. Task F03 supplies `useChat`'s `onStart` and pumps events into `applyEvent`.
 */
function AppShell() {
  const { strings } = useLanguage()
  const chat = useChat()

  return (
    <main className="app">
      <header className="app__header">
        <h1 className="app__title">{strings.appTitle}</h1>
        <LanguageToggle />
      </header>
      <ChatView turns={chat.turns} isStreaming={chat.isStreaming} onSubmit={chat.submit} />
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
