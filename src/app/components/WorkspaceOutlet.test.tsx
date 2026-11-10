// A page starts afresh when its tab switches workspace.
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const workspace = vi.hoisted(() => ({ id: 'pavillon' }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => ({ currentOrgId: workspace.id }) }));

import { WorkspaceOutlet } from './WorkspaceOutlet';

/** A page holding something of its own: a filter typed into it. */
function Page() {
  const [filter, setFilter] = useState('');
  return <input aria-label="Filter" value={filter} onChange={(e) => setFilter(e.target.value)} />;
}

function app() {
  return (
    <MemoryRouter initialEntries={['/leads']}>
      <Routes>
        <Route element={<WorkspaceOutlet />}>
          <Route path="/leads" element={<Page />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('a page when the workspace changes', () => {
  it('keeps what it holds while the workspace stays, and starts afresh in another', () => {
    const { rerender } = render(app());
    fireEvent.change(screen.getByLabelText('Filter'), { target: { value: 'assigned to Anna' } });

    rerender(app());
    expect(screen.getByLabelText('Filter')).toHaveValue('assigned to Anna');

    workspace.id = 'lac-leman';
    rerender(app());
    expect(screen.getByLabelText('Filter')).toHaveValue('');
  });
});
