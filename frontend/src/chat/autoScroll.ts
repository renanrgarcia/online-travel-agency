/**
 * How close to the bottom (in px) still counts as "following along". Anything further up is treated
 * as the user having deliberately scrolled back, and auto-scroll stops fighting them.
 */
export const FOLLOW_THRESHOLD_PX = 48

export interface ScrollPosition {
  scrollTop: number
  scrollHeight: number
  clientHeight: number
}

/**
 * Pure so it can be tested without layout — jsdom reports every dimension as 0, so a test that
 * exercised this through a real element would assert nothing.
 */
export function isPinnedToBottom(position: ScrollPosition): boolean {
  const distanceFromBottom = position.scrollHeight - position.scrollTop - position.clientHeight
  return distanceFromBottom <= FOLLOW_THRESHOLD_PX
}
