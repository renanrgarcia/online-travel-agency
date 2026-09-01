import { ChatView } from './chat/ChatView'
import { useSearchChat } from './chat/useSearchChat'
import { LanguageProvider } from './i18n/LanguageProvider'
import { LanguageToggle } from './i18n/LanguageToggle'
import { useLanguage } from './i18n/LanguageProvider'

function AppShell() {
  const { strings } = useLanguage()
  const chat = useSearchChat()

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
