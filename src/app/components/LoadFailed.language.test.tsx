// A read that failed says so in the person's language, on every page that uses the shared
// state, and still offers to try again.

import { beforeAll, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider, loadCatalogue } from '@/app/i18n';
import { LoadFailed } from './LoadFailed';

beforeAll(async () => {
  await loadCatalogue('fr');
});

describe('a failed read', () => {
  it('says what failed and offers a retry, in French', async () => {
    localStorage.setItem('cadence.language', 'fr');
    const retry = vi.fn();
    try {
      render(<I18nProvider><LoadFailed what="Les membres" onRetry={retry} /></I18nProvider>);
      expect(screen.getByText('Chargement impossible : Les membres.')).toBeInTheDocument();
      expect(screen.getByText('Le serveur était injoignable ou n’a pas répondu.')).toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Réessayer' }));
      expect(retry).toHaveBeenCalledTimes(1);
    } finally {
      localStorage.removeItem('cadence.language');
    }
  });

  it('reads as it always did in English', () => {
    render(<LoadFailed what="Members" onRetry={() => {}} />);
    expect(screen.getByText('Members could not be loaded.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
