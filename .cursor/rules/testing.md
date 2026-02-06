# Testing Standards

## Backend Testing (xUnit + Moq + FluentAssertions)

### Test File Structure
```
backend/tests/ACI.Application.Tests/
└── Services/
    ├── ContactServiceTests.cs
    ├── LeadServiceTests.cs
    └── AuthServiceTests.cs
```

### Test Naming Convention
```csharp
[Fact]
public async Task MethodName_StateUnderTest_ExpectedBehavior()
{
    // Example:
    // GetByIdAsync_WithValidId_ReturnsContact
    // CreateAsync_WithInvalidEmail_ReturnsValidationError
}
```

### Test Structure (Arrange-Act-Assert)
```csharp
[Fact]
public async Task GetByIdAsync_WithValidId_ReturnsContact()
{
    // Arrange
    var mockRepo = new Mock<IContactRepository>();
    var contact = new Contact { Id = Guid.NewGuid(), Name = "Test" };
    mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), contact.Id))
            .ReturnsAsync(contact);
    
    var service = new ContactService(mockRepo.Object, _logger);
    
    // Act
    var result = await service.GetByIdAsync("user-1", contact.Id);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
    result.Value.Name.Should().Be("Test");
}
```

### What to Test
- ✅ Happy path (success scenarios)
- ✅ Validation failures (invalid input)
- ✅ Not found scenarios
- ✅ Authorization failures
- ✅ Edge cases (empty lists, null values)

### What NOT to Test
- ❌ Framework code (EF Core, ASP.NET Core)
- ❌ Third-party libraries
- ❌ Simple mapping logic

## Frontend Testing (Vitest + React Testing Library + MSW)

### Test File Structure
```
src/
├── app/
│   └── components/
│       ├── ErrorBoundary.tsx
│       └── ErrorBoundary.test.tsx
└── test/
    ├── mocks/
    │   └── handlers.ts
    ├── setup.ts
    └── utils.tsx
```

### Component Test Example
```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ContactCard } from './ContactCard';

describe('ContactCard', () => {
  it('renders contact name', () => {
    const contact = { id: '1', name: 'John Doe', email: 'john@example.com' };
    render(<ContactCard contact={contact} onEdit={vi.fn()} onDelete={vi.fn()} />);
    
    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });
  
  it('calls onEdit when edit button clicked', async () => {
    const onEdit = vi.fn();
    const contact = { id: '1', name: 'John Doe', email: 'john@example.com' };
    render(<ContactCard contact={contact} onEdit={onEdit} onDelete={vi.fn()} />);
    
    await userEvent.click(screen.getByRole('button', { name: /edit/i }));
    
    expect(onEdit).toHaveBeenCalledWith('1');
  });
});
```

### MSW for API Mocking
```typescript
// test/mocks/handlers.ts
import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/api/contacts', () => {
    return HttpResponse.json([
      { id: '1', name: 'John Doe', email: 'john@example.com' },
    ]);
  }),
];
```

## Running Tests

### Backend
```bash
cd backend
dotnet test
```

### Frontend
```bash
npm test           # Run once
npm run test:watch # Watch mode
npm run test:ui    # Visual UI
```
