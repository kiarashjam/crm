// The strip used to offer "Turn off" for automatic status updates. That switch
// lived in this browser's storage, and the server now sets the status for every
// writer (an e-signed contract at night has no browser at all), so the switch
// would have claimed something the product does not do.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusSyncStrip } from './StatusSyncStrip';
import { deriveStage } from '../leadStatusSync';

describe('StatusSyncStrip', () => {
  it('says the status follows the pipeline, and offers no switch to stop it', () => {
    render(<StatusSyncStrip drift={null} onApply={() => {}} />);

    expect(screen.getByText(/follows the pipeline automatically/i)).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('still offers to bring a drifted status in line', () => {
    render(
      <StatusSyncStrip
        drift={{
          derived: deriveStage({ contractSigned: 'yes' }),
          suggested: { name: 'Signed', match: 'exact', canonical: 'signed' },
          currentStatus: 'Contacted',
        }}
        onApply={() => {}}
      />,
    );

    expect(screen.getByRole('button', { name: /set to signed/i })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /turn on|turn off/i })).toBeNull();
  });
});
