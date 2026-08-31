/**
 * Payloads captured verbatim from a running `FlightAi.Api` (the demo `OfflineChatClient` answering
 * "cheapest flight from São Paulo to Lisbon"). Recorded rather than hand-written so the tests assert
 * against what the server really sends.
 *
 * These stay valid indefinitely: the mock connectors and the scoring weights are deterministic, so
 * the same query always produces these exact offers in this exact order.
 */

export const PARSED_INTENT_JSON =
  '{"origin":"GRU","destination":"LIS","departureDate":"2027-03-12","passengerCount":2,"language":"en"}'

export const SUPPLIER_RESULT_GDS_JSON =
  '{"supplierName":"GDS","status":"Succeeded","offerCount":2,"reason":null}'

export const SUPPLIER_RESULT_NDC_JSON =
  '{"supplierName":"NDC","status":"Succeeded","offerCount":2,"reason":null}'

export const SUPPLIER_RESULT_LCC_FAILED_JSON =
  '{"supplierName":"LCC","status":"TimedOut","offerCount":0,"reason":"exceeded 5s timeout"}'

export const RANKED_OFFERS_JSON =
  '[{"rank":1,"offerId":"LCC-002","price":590,"currency":"USD","durationMinutes":480,"stops":1,"refundable":false,"score":1071},' +
  '{"rank":2,"offerId":"LCC-001","price":410,"currency":"USD","durationMinutes":660,"stops":2,"refundable":false,"score":1072},' +
  '{"rank":3,"offerId":"GDS-001","price":730,"currency":"USD","durationMinutes":420,"stops":1,"refundable":true,"score":1151}]'

export const EXPLANATION_JSON =
  '{"text":"The best value is $590.00, taking 8h with 1 stop (non-refundable).","raw":"The best value is {{PRICE_LCC-002}}, taking {{DURATION_LCC-002}} with {{STOPS_LCC-002}} ({{REFUNDABLE_LCC-002}}).","isClean":true}'

export const ERROR_JSON = '{"message":"missing origin"}'

/** Intent for a Portuguese query — the accented city name is the point (F01 E4). */
export const PARSED_INTENT_ACCENTED_JSON =
  '{"origin":"São Paulo","destination":"Lisboa","departureDate":"2027-03-12","passengerCount":2,"language":"pt-BR"}'
