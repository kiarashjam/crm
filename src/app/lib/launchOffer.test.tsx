import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LAUNCH_OFFER_COPY, LAUNCH_OFFER_ENDS_AT_MS, LAUNCH_OFFER_LAST_DAY, isLaunchOfferActive } from './launchOffer';
import { LaunchOfferBadge, LaunchOfferStrip } from '@/app/marketing/components/LaunchOffer';
import MarketingPageEn from '@/app/marketing/pages/MarketingPageEn';
import { headForPath } from '@/app/marketing/seo';
import { buildLlmsTxt } from '@/app/marketing/llms';

describe('isLaunchOfferActive', () => {
  it('runs through 31 December 2026, 23:59:59 in Zurich', () => {
    expect(isLaunchOfferActive(new Date('2026-11-25T09:00:00+01:00'))).toBe(true);
    expect(isLaunchOfferActive(new Date('2026-12-31T23:59:59+01:00'))).toBe(true);
    expect(isLaunchOfferActive(new Date('2026-12-31T23:59:59.999+01:00'))).toBe(true);
    expect(isLaunchOfferActive(Date.parse('2026-12-31T22:59:59Z'))).toBe(true);
  });

  it('has ended from 1 January 2027, 00:00 in Zurich, and stays ended', () => {
    expect(isLaunchOfferActive(new Date('2027-01-01T00:00:00+01:00'))).toBe(false);
    expect(isLaunchOfferActive(Date.parse('2026-12-31T23:00:00Z'))).toBe(false);
    expect(isLaunchOfferActive(new Date('2030-06-01T00:00:00Z'))).toBe(false);
    expect(LAUNCH_OFFER_ENDS_AT_MS).toBe(Date.UTC(2026, 11, 31, 23, 0, 0));
    expect(LAUNCH_OFFER_LAST_DAY).toBe('2026-12-31');
  });

  it('treats an invalid date as no offer', () => {
    expect(isLaunchOfferActive(new Date('not a date'))).toBe(false);
  });

  it('has its words in every language', () => {
    for (const copy of Object.values(LAUNCH_OFFER_COPY)) {
      expect(copy.badge).toMatch(/2026/);
      expect(copy.strip).toMatch(/2026/);
      expect(copy.cta.length).toBeGreaterThan(0);
    }
  });
});

describe('the offer on the site', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function at(iso: string) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(iso));
  }

  it('shows the strip and badge before the end, and nothing after', () => {
    at('2026-12-31T22:30:00Z');
    const { unmount } = render(
      <MemoryRouter>
        <LaunchOfferBadge lang="fr" />
        <LaunchOfferStrip lang="en" />
      </MemoryRouter>,
    );
    expect(screen.getByText('Gratuit jusqu’au 31 décembre 2026')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Start free/ })).toHaveAttribute('data-track', 'cta-signup');
    unmount();

    at('2027-01-01T08:00:00+01:00');
    const after = render(
      <MemoryRouter>
        <LaunchOfferBadge lang="fr" />
        <LaunchOfferStrip lang="en" />
      </MemoryRouter>,
    );
    expect(after.container).toBeEmptyDOMElement();
  });

  it('turns the pricing page into its after-offer version, head included', () => {
    at('2026-12-01T12:00:00Z');
    const during = render(
      <MemoryRouter initialEntries={['/pricing']}>
        <MarketingPageEn id="pricing" />
      </MemoryRouter>,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('CadenceE is free until 31 December 2026');
    expect(screen.getAllByRole('link', { name: /Start free/ }).length).toBeGreaterThan(0);
    during.unmount();

    at('2027-01-01T00:00:01+01:00');
    render(
      <MemoryRouter initialEntries={['/pricing']}>
        <MarketingPageEn id="pricing" />
      </MemoryRouter>,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('CadenceE pricing for clubs and sales teams');
    expect(document.body.textContent).not.toMatch(/Free until|Start free/);
    expect(screen.getAllByRole('link', { name: /Create your workspace/ })[0]).toHaveAttribute('data-track', 'cta-signup');

    const head = headForPath('/pricing', Date.now());
    expect(head.title).toBe('CadenceE Pricing for Clubs and Sales Teams');
    expect(head.metas.find((m) => m.property === 'og:image')?.content).toBe('https://www.cadencee.ai/og/pricing-en.png');
  });

  it('uses the offer OG image for home and pricing only while it runs', () => {
    const og = (path: string, now: number) => headForPath(path, now).metas.find((m) => m.property === 'og:image')?.content;
    const during = Date.parse('2026-11-25T12:00:00Z');
    const after = Date.parse('2027-02-01T12:00:00Z');
    expect(og('/', during)).toBe('https://www.cadencee.ai/og/home-en-offer.png');
    expect(og('/de/preise', during)).toBe('https://www.cadencee.ai/og/pricing-de-offer.png');
    expect(og('/sales-crm', during)).toBe('https://www.cadencee.ai/og/sales-en.png');
    expect(og('/', after)).toBe('https://www.cadencee.ai/og/home-en.png');
  });

  it('mentions the offer in llms.txt only while it runs', () => {
    expect(buildLlmsTxt(Date.parse('2026-11-25T12:00:00Z'))).toMatch(/free until 2026-12-31/);
    expect(buildLlmsTxt(Date.parse('2027-01-05T12:00:00Z'))).not.toMatch(/2026/);
  });
});
