import { useLanguage } from './LanguageProvider'
import { LANGUAGE_LABELS, LANGUAGES } from './strings'

/**
 * Always visible, not tucked into a menu — the target market is bilingual from the first screen, not
 * after finding a settings page.
 */
export function LanguageToggle() {
  const { language, strings, setLanguage } = useLanguage()

  return (
    <div className="language-toggle" role="group" aria-label={strings.languageToggleLabel}>
      {LANGUAGES.map((candidate) => (
        <button
          key={candidate}
          type="button"
          className="language-toggle__option"
          aria-pressed={candidate === language}
          onClick={() => setLanguage(candidate)}
        >
          {LANGUAGE_LABELS[candidate]}
        </button>
      ))}
    </div>
  )
}
