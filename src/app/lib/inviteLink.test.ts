import { describe, expect, it } from 'vitest';
import { invitationToResume } from './inviteLink';

describe('invitationToResume', () => {
  it('returns to the invitation link, token and all, after signing in', () => {
    const state = { from: { pathname: '/invites/accept', search: '?token=abc_123-x' } };
    expect(invitationToResume(state)).toBe('/invites/accept?token=abc_123-x');
  });

  it('leaves every other page to the usual landing', () => {
    expect(invitationToResume({ from: { pathname: '/leads', search: '' } })).toBeNull();
    expect(invitationToResume({ from: '/invites/accept?token=x' })).toBeNull();
    expect(invitationToResume(null)).toBeNull();
    expect(invitationToResume(undefined)).toBeNull();
  });

  it('cannot be pointed out of the app', () => {
    expect(invitationToResume({ from: { pathname: '//evil.example/invites/accept', search: '' } })).toBeNull();
    expect(invitationToResume({ from: { pathname: '/invites/accept', search: '//evil.example' } }))
      .toBe('/invites/accept');
  });
});
