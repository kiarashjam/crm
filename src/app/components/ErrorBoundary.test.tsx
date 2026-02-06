import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ErrorBoundary, withErrorBoundary } from './ErrorBoundary';

// Component that throws an error
const ThrowError = ({ shouldThrow = true }: { shouldThrow?: boolean }) => {
  if (shouldThrow) {
    throw new Error('Test error message');
  }
  return <div>No error</div>;
};

describe('ErrorBoundary', () => {
  // Suppress console.error for cleaner test output
  const originalError = console.error;
  beforeEach(() => {
    console.error = vi.fn();
  });
  afterEach(() => {
    console.error = originalError;
  });

  it('renders children when there is no error', () => {
    render(
      <ErrorBoundary>
        <div>Test content</div>
      </ErrorBoundary>
    );
    
    expect(screen.getByText('Test content')).toBeInTheDocument();
  });

  it('renders error UI when a child throws', () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );
    
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText(/An unexpected error occurred/)).toBeInTheDocument();
  });

  it('renders custom fallback when provided', () => {
    render(
      <ErrorBoundary fallback={<div>Custom error message</div>}>
        <ThrowError />
      </ErrorBoundary>
    );
    
    expect(screen.getByText('Custom error message')).toBeInTheDocument();
    expect(screen.queryByText('Something went wrong')).not.toBeInTheDocument();
  });

  it('shows Try Again button that calls handleReset', () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );
    
    // Error UI should be shown
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    
    // Try Again button should be present
    const tryAgainButton = screen.getByText('Try Again');
    expect(tryAgainButton).toBeInTheDocument();
    
    // Clicking it will attempt to re-render children
    // Since child still throws, it will show error UI again
    fireEvent.click(tryAgainButton);
    
    // Error UI is still shown because child still throws
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('shows Refresh Page button', () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );
    
    expect(screen.getByText('Refresh Page')).toBeInTheDocument();
  });

  it('logs error to console', () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );
    
    expect(console.error).toHaveBeenCalled();
  });
});

describe('withErrorBoundary HOC', () => {
  const originalError = console.error;
  beforeEach(() => {
    console.error = vi.fn();
  });
  afterEach(() => {
    console.error = originalError;
  });

  it('wraps component with error boundary', () => {
    const WrappedComponent = withErrorBoundary(ThrowError);
    
    render(<WrappedComponent />);
    
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('uses custom fallback in HOC', () => {
    const WrappedComponent = withErrorBoundary(
      ThrowError,
      <div>HOC custom fallback</div>
    );
    
    render(<WrappedComponent />);
    
    expect(screen.getByText('HOC custom fallback')).toBeInTheDocument();
  });
});
