/**
 * UI chrome strings, en / pt-BR. Deliberately not an i18n library (F07's locked decision) — two
 * languages and a flat set of keys is a lookup table, not a dependency.
 *
 * This covers only the chrome F01/F02 actually built. Search-result content (offers, explanations,
 * booking, degraded states) is out of scope here — those come from the server already in the
 * traveller's language, or don't exist as UI yet.
 */

export type Language = 'en' | 'pt-BR'

export const LANGUAGES: readonly Language[] = ['en', 'pt-BR']

export interface Strings {
  appTitle: string
  languageToggleLabel: string
  composerLabel: string
  composerPlaceholder: string
  composerSubmit: string
  composerEmptyError: string
  composerStreamingHint: string
  emptyStateTitle: string
  emptyStateBody: string
  emptyStateSuggestion: string
  youAskedLabel: string
  resultsLabel: string
  stageUnderstood: string
  stageSuppliers: string
  stageOffers: string
  stageWhy: string
  searching: string
  waitingForOffers: string
  waitingForExplanation: string
  traveller: string
  travellers: string
  connectionLost: string
}

const en: Strings = {
  appTitle: 'FlightAi',
  languageToggleLabel: 'Language',
  composerLabel: 'Search for a flight',
  composerPlaceholder: 'Where do you want to go?',
  composerSubmit: 'Search',
  composerEmptyError: 'Enter a search first',
  composerStreamingHint: 'One search at a time — this one is still running.',
  emptyStateTitle: 'Search flights in your own words',
  emptyStateBody:
    'Describe the trip you want. Offers are ranked by a deterministic scorer, and the explanation ' +
    'is written from resolved values — never numbers a model made up.',
  emptyStateSuggestion: 'cheapest flight from São Paulo to Lisbon',
  youAskedLabel: 'You asked',
  resultsLabel: 'Search results',
  stageUnderstood: 'Understood',
  stageSuppliers: 'Suppliers',
  stageOffers: 'Offers',
  stageWhy: 'Why',
  searching: 'Understanding your search…',
  waitingForOffers: 'Checking suppliers…',
  waitingForExplanation: 'Writing an explanation…',
  traveller: 'traveller',
  travellers: 'travellers',
  connectionLost: 'Connection lost. Try your search again.',
}

const ptBR: Strings = {
  appTitle: 'FlightAi',
  languageToggleLabel: 'Idioma',
  composerLabel: 'Buscar um voo',
  composerPlaceholder: 'Para onde você quer ir?',
  composerSubmit: 'Buscar',
  composerEmptyError: 'Digite uma busca primeiro',
  composerStreamingHint: 'Uma busca por vez — esta ainda está em andamento.',
  emptyStateTitle: 'Busque voos com suas próprias palavras',
  emptyStateBody:
    'Descreva a viagem que você quer. As ofertas são ranqueadas por um algoritmo determinístico, e a ' +
    'explicação é escrita a partir de valores resolvidos — nunca números inventados por um modelo.',
  emptyStateSuggestion: 'voo mais barato de São Paulo para Lisboa',
  youAskedLabel: 'Você perguntou',
  resultsLabel: 'Resultados da busca',
  stageUnderstood: 'Entendido',
  stageSuppliers: 'Fornecedores',
  stageOffers: 'Ofertas',
  stageWhy: 'Por quê',
  searching: 'Entendendo sua busca…',
  waitingForOffers: 'Consultando fornecedores…',
  waitingForExplanation: 'Escrevendo uma explicação…',
  traveller: 'passageiro',
  travellers: 'passageiros',
  connectionLost: 'Conexão perdida. Tente sua busca novamente.',
}

export const STRINGS: Record<Language, Strings> = { en, 'pt-BR': ptBR }

export const LANGUAGE_LABELS: Record<Language, string> = { en: 'EN', 'pt-BR': 'PT-BR' }
