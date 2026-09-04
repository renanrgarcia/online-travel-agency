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
  supplierStatusSucceeded: string
  supplierStatusPartialSuccess: string
  supplierStatusFailed: string
  supplierStatusTimedOut: string
  supplierStatusCancelled: string
  supplierStatusSkippedCircuitOpen: string
  supplierStatusSkippedBudgetExhausted: string
  noOffersFound: string
  explanationUnavailable: string
  explanationShowRaw: string
  explanationRawLabel: string
  offerCountOne: string
  offerCountMany: string
  traveller: string
  travellers: string
  connectionLost: string
  aiUnavailable: string
  missingDepartureDate: string
  newSearch: string
  offerRank: string
  offerDuration: string
  offerRefundable: string
  offerNonRefundable: string
  stopsNonstop: string
  stopsOne: string
  stopsMany: string
  comparisonTitle: string
  comparisonPrice: string
  comparisonDuration: string
  comparisonStops: string
  comparisonRefundable: string
  bookOffer: string
  bookingTravellerEmailLabel: string
  bookingTravellerEmailPlaceholder: string
  bookingConfirm: string
  bookingCancel: string
  bookingEmailRequired: string
  bookingSubmitting: string
  bookingStepAuthorizingPayment: string
  bookingStepCreatingOrder: string
  bookingStepIssuingTicket: string
  bookingStepSendingConfirmation: string
  bookingStepCompensating: string
  bookingBookedTitle: string
  bookingTicketNumber: string
  bookingFailedTitle: string
  bookingCompensated: string
  bookingCompensationFailed: string
  bookingNotCompensated: string
  bookingErrorTitle: string
  bookingErrorNotFound: string
  bookingErrorNetwork: string
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
  emptyStateSuggestion: 'cheapest flight from São Paulo to Lisbon on March 12, 2027',
  youAskedLabel: 'You asked',
  resultsLabel: 'Search results',
  stageUnderstood: 'Understood',
  stageSuppliers: 'Suppliers',
  stageOffers: 'Offers',
  stageWhy: 'Why',
  searching: 'Understanding your search…',
  waitingForOffers: 'Checking suppliers…',
  waitingForExplanation: 'Writing an explanation…',
  supplierStatusSucceeded: 'Succeeded',
  supplierStatusPartialSuccess: 'Partial results',
  supplierStatusFailed: 'Failed',
  supplierStatusTimedOut: 'Timed out',
  supplierStatusCancelled: 'Cancelled',
  supplierStatusSkippedCircuitOpen: 'Skipped (temporarily paused)',
  supplierStatusSkippedBudgetExhausted: 'Skipped (budget reached)',
  noOffersFound: 'No offers found for this search.',
  explanationUnavailable: "An explanation isn't available for this search.",
  explanationShowRaw: 'Show raw model output (debug)',
  explanationRawLabel: 'Raw, unrendered model output:',
  offerCountOne: '1 offer',
  offerCountMany: '{n} offers',
  traveller: 'traveller',
  travellers: 'travellers',
  connectionLost: 'Connection lost. Try your search again.',
  aiUnavailable: 'The AI service is temporarily unavailable. Try again later.',
  missingDepartureDate: "Let me know when you'd like to travel — I need a departure date to search.",
  newSearch: 'New search',
  offerRank: 'Rank',
  offerDuration: 'Duration',
  offerRefundable: 'Refundable',
  offerNonRefundable: 'Non-refundable',
  stopsNonstop: 'nonstop',
  stopsOne: '1 stop',
  stopsMany: '{n} stops',
  comparisonTitle: 'Compare',
  comparisonPrice: 'Price',
  comparisonDuration: 'Duration',
  comparisonStops: 'Stops',
  comparisonRefundable: 'Refundable',
  bookOffer: 'Book this offer',
  bookingTravellerEmailLabel: 'Traveller email',
  bookingTravellerEmailPlaceholder: 'you@example.com',
  bookingConfirm: 'Confirm booking',
  bookingCancel: 'Cancel',
  bookingEmailRequired: 'Enter an email first',
  bookingSubmitting: 'Starting your booking…',
  bookingStepAuthorizingPayment: 'Authorizing payment…',
  bookingStepCreatingOrder: 'Creating your order…',
  bookingStepIssuingTicket: 'Issuing your ticket…',
  bookingStepSendingConfirmation: 'Sending confirmation…',
  bookingStepCompensating: 'Rolling back this booking…',
  bookingBookedTitle: 'Booked',
  bookingTicketNumber: 'Ticket number',
  bookingFailedTitle: 'Booking failed',
  bookingCompensated: 'Nothing was charged — any payment authorization and order were undone.',
  bookingCompensationFailed: 'The rollback itself failed. This needs manual follow-up — do not retry.',
  bookingNotCompensated: 'The booking stopped before anything needed to be undone.',
  bookingErrorTitle: 'Something went wrong',
  bookingErrorNotFound: 'This booking could not be found.',
  bookingErrorNetwork: 'Could not reach the booking service. Check your connection and try again.',
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
  emptyStateSuggestion: 'voo mais barato de São Paulo para Lisboa em 12 de março de 2027',
  youAskedLabel: 'Você perguntou',
  resultsLabel: 'Resultados da busca',
  stageUnderstood: 'Entendido',
  stageSuppliers: 'Fornecedores',
  stageOffers: 'Ofertas',
  stageWhy: 'Por quê',
  searching: 'Entendendo sua busca…',
  waitingForOffers: 'Consultando fornecedores…',
  waitingForExplanation: 'Escrevendo uma explicação…',
  supplierStatusSucceeded: 'Concluído',
  supplierStatusPartialSuccess: 'Resultados parciais',
  supplierStatusFailed: 'Falhou',
  supplierStatusTimedOut: 'Tempo esgotado',
  supplierStatusCancelled: 'Cancelado',
  supplierStatusSkippedCircuitOpen: 'Ignorado (pausado temporariamente)',
  supplierStatusSkippedBudgetExhausted: 'Ignorado (orçamento esgotado)',
  noOffersFound: 'Nenhuma oferta encontrada para esta busca.',
  explanationUnavailable: 'Uma explicação não está disponível para esta busca.',
  explanationShowRaw: 'Mostrar saída bruta do modelo (depuração)',
  explanationRawLabel: 'Saída bruta do modelo, sem processamento:',
  offerCountOne: '1 oferta',
  offerCountMany: '{n} ofertas',
  traveller: 'passageiro',
  travellers: 'passageiros',
  connectionLost: 'Conexão perdida. Tente sua busca novamente.',
  aiUnavailable: 'O serviço de IA está temporariamente indisponível. Tente novamente mais tarde.',
  missingDepartureDate: 'Me diga quando você gostaria de viajar — preciso de uma data de partida para buscar.',
  newSearch: 'Nova busca',
  offerRank: 'Posição',
  offerDuration: 'Duração',
  offerRefundable: 'Reembolsável',
  offerNonRefundable: 'Não reembolsável',
  stopsNonstop: 'sem escalas',
  stopsOne: '1 escala',
  stopsMany: '{n} escalas',
  comparisonTitle: 'Comparar',
  comparisonPrice: 'Preço',
  comparisonDuration: 'Duração',
  comparisonStops: 'Escalas',
  comparisonRefundable: 'Reembolsável',
  bookOffer: 'Reservar esta oferta',
  bookingTravellerEmailLabel: 'E-mail do passageiro',
  bookingTravellerEmailPlaceholder: 'voce@exemplo.com',
  bookingConfirm: 'Confirmar reserva',
  bookingCancel: 'Cancelar',
  bookingEmailRequired: 'Digite um e-mail primeiro',
  bookingSubmitting: 'Iniciando sua reserva…',
  bookingStepAuthorizingPayment: 'Autorizando pagamento…',
  bookingStepCreatingOrder: 'Criando seu pedido…',
  bookingStepIssuingTicket: 'Emitindo sua passagem…',
  bookingStepSendingConfirmation: 'Enviando confirmação…',
  bookingStepCompensating: 'Revertendo esta reserva…',
  bookingBookedTitle: 'Reservado',
  bookingTicketNumber: 'Número da passagem',
  bookingFailedTitle: 'Falha na reserva',
  bookingCompensated: 'Nada foi cobrado — qualquer autorização de pagamento e pedido foram desfeitos.',
  bookingCompensationFailed: 'A reversão em si falhou. Isso precisa de acompanhamento manual — não tente novamente.',
  bookingNotCompensated: 'A reserva parou antes que algo precisasse ser desfeito.',
  bookingErrorTitle: 'Algo deu errado',
  bookingErrorNotFound: 'Esta reserva não foi encontrada.',
  bookingErrorNetwork: 'Não foi possível acessar o serviço de reservas. Verifique sua conexão e tente novamente.',
}

export const STRINGS: Record<Language, Strings> = { en, 'pt-BR': ptBR }

export const LANGUAGE_LABELS: Record<Language, string> = { en: 'EN', 'pt-BR': 'PT-BR' }

/**
 * Maps `parsed-intent.language` (a BCP-47-ish tag the intent agent inferred, e.g. `en`, `pt-BR`) onto
 * this UI's own two-value {@link Language}. Only pt-BR and en are in scope (F07's own out-of-scope
 * list) — anything else falls back to `en` rather than rendering chrome in a language with no strings.
 */
export function toUiLanguage(raw: string): Language {
  return raw.toLowerCase().startsWith('pt') ? 'pt-BR' : 'en'
}
