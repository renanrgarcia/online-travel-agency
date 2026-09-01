import { useLanguage } from '../i18n/LanguageProvider'

export interface EmptyStateProps {
  onPickSuggestion: (query: string) => void
}

export function EmptyState({ onPickSuggestion }: EmptyStateProps) {
  const { strings } = useLanguage()

  return (
    <div className="empty-state">
      <h2 className="empty-state__title">{strings.emptyStateTitle}</h2>
      <p className="empty-state__body">{strings.emptyStateBody}</p>
      <button
        className="empty-state__suggestion"
        type="button"
        onClick={() => onPickSuggestion(strings.emptyStateSuggestion)}
      >
        {strings.emptyStateSuggestion}
      </button>
    </div>
  )
}
