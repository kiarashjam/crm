// The "Why they did not proceed" card, rendered.
//
// The breakdown underneath it was always right; the card was not. It drew every
// row under the drop-out columns, so a rejection — counted under REJECTION codes —
// matched none of them: the row showed as dashes with a total, the recorded reason
// appeared nowhere on the page, and the footnote counted it as an unexplained
// drop-out. A pure-function test could not have caught that, because the fault
// was entirely in which columns the page chose to draw. So this renders the page.

import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { Lead } from '@/app/api/types';
import type { LeadPipeline } from '../leads/leadPipeline';
import { SalesTrackerReport } from './SalesTrackerReport';

let n = 0;
const lead = (pipeline: LeadPipeline): Lead => {
  n += 1;
  return {
    id: `lead-${n}`,
    name: `Lead ${n}`,
    email: `lead${n}@example.ch`,
    status: 'New',
    pipelineState: JSON.stringify(pipeline),
  };
};

const DROPPED = lead({ contractStatus: 'no_longer_interested', dropoutReason: 'too_soon', dropoutReasonPhase: 3 });
const REJECTED = lead({ contractStatus: 'profile_rejected', rejectionReason: 'does_not_meet_criteria' });
const REJECTED_NO_REASON = lead({ contractStatus: 'profile_rejected' });

/** The value under `column` in the body row whose first cell starts with `rowStart`. */
function cell(table: HTMLElement, rowStart: string, column: string): string {
  const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent?.trim());
  const col = headers.indexOf(column);
  expect(col, `no column "${column}" in ${table.getAttribute('aria-label')}`).toBeGreaterThan(-1);
  const row = within(table).getAllByRole('row')
    .find((r) => r.querySelector('td')?.textContent?.trim().startsWith(rowStart));
  expect(row, `no row starting "${rowStart}"`).toBeTruthy();
  return row!.querySelectorAll('td')[col]!.textContent!.trim();
}

/** Body rows only — the header row is a row too. */
const bodyRows = (table: HTMLElement) => within(table).getAllByRole('row').filter((r) => r.querySelector('td'));

describe('the rejection reasons reach the page', () => {
  it('a recorded rejection reason is shown, with its count, in the rejection table', () => {
    render(<SalesTrackerReport leads={[DROPPED, REJECTED]} />);

    const rejections = screen.getByRole('table', { name: 'Rejection reasons by phase' });
    // The exact thing that used to be absent from the whole page.
    expect(within(rejections).getByRole('columnheader', { name: 'Does not meet membership criteria' }))
      .toBeInTheDocument();
    expect(cell(rejections, 'Contract', 'Does not meet membership criteria')).toBe('1');
    expect(cell(rejections, 'Contract', 'Total')).toBe('1');
  });

  it('the drop-out table holds only drop-outs — no row of dashes for a rejection', () => {
    render(<SalesTrackerReport leads={[DROPPED, REJECTED]} />);

    const dropouts = screen.getByRole('table', { name: 'Drop-out reasons by phase' });
    // One phase-3 row, not two: the rejection used to sit here as a second
    // "Contract" row with every column blank.
    expect(bodyRows(dropouts)).toHaveLength(1);
    expect(cell(dropouts, 'Contract', 'Unable to Commit or Plan This Far in Advance')).toBe('1');
    expect(within(dropouts).queryByText('Does not meet membership criteria')).toBeNull();
  });

  it('the two vocabularies never share a table', () => {
    render(<SalesTrackerReport leads={[DROPPED, REJECTED]} />);

    const dropoutHeads = within(screen.getByRole('table', { name: 'Drop-out reasons by phase' }))
      .getAllByRole('columnheader').map((h) => h.textContent);
    const rejectionHeads = within(screen.getByRole('table', { name: 'Rejection reasons by phase' }))
      .getAllByRole('columnheader').map((h) => h.textContent);

    expect(dropoutHeads).toContain('Membership Fee Considered Too High');
    expect(dropoutHeads).not.toContain('Does not meet membership criteria');
    expect(rejectionHeads).toContain('Does not meet membership criteria');
    expect(rejectionHeads).not.toContain('Membership Fee Considered Too High');
  });

  it('a rejection with no reason is owed by the rejection table, not counted as a drop-out', () => {
    render(<SalesTrackerReport leads={[DROPPED, REJECTED_NO_REASON]} />);

    expect(screen.getByText('1 of 1 rejections have no reason recorded yet.')).toBeInTheDocument();
    // The drop-out has its reason, so the drop-out footnote must not appear —
    // it used to read "1 of 2 drop-outs", counting our own decision as theirs.
    expect(screen.queryByText(/drop-outs have no reason recorded yet/)).toBeNull();
  });

  it('with no rejections, there is no rejection table at all', () => {
    render(<SalesTrackerReport leads={[DROPPED]} />);

    expect(screen.queryByRole('table', { name: 'Rejection reasons by phase' })).toBeNull();
    expect(screen.queryByText('Turned down by us')).toBeNull();
    expect(screen.getByRole('table', { name: 'Drop-out reasons by phase' })).toBeInTheDocument();
  });
});
