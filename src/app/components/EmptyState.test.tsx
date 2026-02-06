import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { Inbox } from 'lucide-react';
import EmptyState from './EmptyState';

const renderWithRouter = (ui: React.ReactElement) => {
  return render(<BrowserRouter>{ui}</BrowserRouter>);
};

describe('EmptyState', () => {
  it('renders title and icon', () => {
    renderWithRouter(
      <EmptyState icon={Inbox} title="No items found" />
    );
    
    expect(screen.getByText('No items found')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveAttribute('aria-label', 'No items found');
  });

  it('renders description when provided', () => {
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        description="Start by adding your first item"
      />
    );
    
    expect(screen.getByText('Start by adding your first item')).toBeInTheDocument();
  });

  it('renders action button and handles click', () => {
    const onAction = vi.fn();
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        actionLabel="Add Item"
        onAction={onAction}
      />
    );
    
    const button = screen.getByText('Add Item');
    expect(button).toBeInTheDocument();
    
    fireEvent.click(button);
    expect(onAction).toHaveBeenCalledTimes(1);
  });

  it('renders action link when actionHref is provided', () => {
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        actionLabel="Go to Items"
        actionHref="/items"
      />
    );
    
    const link = screen.getByText('Go to Items');
    expect(link).toBeInTheDocument();
    expect(link.closest('a')).toHaveAttribute('href', '/items');
  });

  it('renders secondary action button', () => {
    const onSecondaryAction = vi.fn();
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        actionLabel="Add Item"
        onAction={() => {}}
        secondaryActionLabel="Import"
        onSecondaryAction={onSecondaryAction}
      />
    );
    
    const secondaryButton = screen.getByText('Import');
    expect(secondaryButton).toBeInTheDocument();
    
    fireEvent.click(secondaryButton);
    expect(onSecondaryAction).toHaveBeenCalledTimes(1);
  });

  it('applies teal variant styles', () => {
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        variant="teal"
        actionLabel="Add"
        onAction={() => {}}
      />
    );
    
    const button = screen.getByText('Add');
    expect(button).toHaveClass('bg-teal-600');
  });

  it('applies custom className', () => {
    renderWithRouter(
      <EmptyState 
        icon={Inbox} 
        title="No items" 
        className="custom-class"
      />
    );
    
    expect(screen.getByRole('status')).toHaveClass('custom-class');
  });

  it('does not render actions section when no actions provided', () => {
    renderWithRouter(
      <EmptyState icon={Inbox} title="No items" />
    );
    
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
