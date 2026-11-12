// Leaving the app for another site: Stripe's own page, where a member pays or saves a card.
// In a module of its own, so a test can stand in for the browser leaving.

/** Opens `url` in place of the app. Only an https address is followed. */
export function leaveFor(url: string): void {
  if (!/^https:\/\//i.test(url)) throw new Error('The payment page address is not a secure one.');
  window.location.assign(url);
}
