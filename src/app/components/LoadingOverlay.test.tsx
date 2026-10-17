import { useState } from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PageLoadingProvider, usePageLoading } from './LoadingOverlay';
import { PageLoader } from './PageLoader';

function Probe({ active }: { active: boolean }) {
  usePageLoading(active);
  return <p>page</p>;
}

function Toggle() {
  const [active, setActive] = useState(true);
  return (
    <>
      <button type="button" onClick={() => setActive(false)}>Done</button>
      <Probe active={active} />
    </>
  );
}

describe('LoadingOverlay', () => {
  it('shows one Loading status while a page is loading, then lets it leave', async () => {
    const user = userEvent.setup();
    render(
      <PageLoadingProvider>
        <Toggle />
      </PageLoadingProvider>,
    );

    expect(screen.getByRole('status', { name: 'Loading' })).toBeInTheDocument();
    expect(screen.getByText('Loading')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Done' }));

    await waitFor(() => {
      expect(screen.queryByRole('status', { name: 'Loading' })).not.toBeInTheDocument();
    });
  });

  it('keeps the overlay up while a route fallback is mounted', async () => {
    const { rerender } = render(
      <PageLoadingProvider>
        <PageLoader />
      </PageLoadingProvider>,
    );

    expect(screen.getByRole('status', { name: 'Loading' })).toBeInTheDocument();

    rerender(
      <PageLoadingProvider>
        <p>ready</p>
      </PageLoadingProvider>,
    );

    await waitFor(() => {
      expect(screen.queryByRole('status', { name: 'Loading' })).not.toBeInTheDocument();
    });
  });
});
