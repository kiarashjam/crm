import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import LoadingSpinner from './LoadingSpinner';

describe('LoadingSpinner', () => {
  it('renders without crashing', () => {
    render(<LoadingSpinner />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders with label', () => {
    render(<LoadingSpinner label="Loading data..." />);
    // Label appears twice - in sr-only span and as visible text
    const labels = screen.getAllByText('Loading data...');
    expect(labels.length).toBeGreaterThanOrEqual(1);
  });

  it('renders small size', () => {
    render(<LoadingSpinner size="sm" />);
    const spinner = screen.getByRole('status');
    expect(spinner).toHaveClass('h-5', 'w-5');
  });

  it('renders medium size by default', () => {
    render(<LoadingSpinner />);
    const spinner = screen.getByRole('status');
    expect(spinner).toHaveClass('h-8', 'w-8');
  });

  it('renders large size', () => {
    render(<LoadingSpinner size="lg" />);
    const spinner = screen.getByRole('status');
    expect(spinner).toHaveClass('h-10', 'w-10');
  });

  it('has accessible status role', () => {
    render(<LoadingSpinner />);
    const status = screen.getByRole('status');
    expect(status).toBeInTheDocument();
  });

  it('has screen reader text', () => {
    render(<LoadingSpinner />);
    // Default label is "Loading"
    expect(screen.getByText('Loading')).toHaveClass('sr-only');
  });
});
