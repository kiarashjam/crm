# Backend Coding Standards

## Architecture
- Follow Clean Architecture: Domain → Application → Infrastructure → WebApi
- Domain layer has NO external dependencies
- Application layer contains business logic, DTOs, interfaces
- Infrastructure implements data access and external services
- WebApi is the entry point (controllers, middleware)

## Naming Conventions
- **Files**: PascalCase (e.g., `ContactService.cs`, `LeadDto.cs`)
- **Classes**: PascalCase with suffix (`ContactService`, `LeadRepository`, `ContactDto`)
- **Interfaces**: `I` prefix (e.g., `IContactService`, `ILeadRepository`)
- **DTOs**: Suffix with `Dto`, `Request`, or `Response`
- **Controllers**: Plural entity name + `Controller` (e.g., `ContactsController`)

## Service Layer Pattern
```csharp
// Use Result<T> pattern for operations that can fail
public async Task<Result<ContactDto>> GetByIdAsync(string userId, Guid id)
{
    _logger.LogInformation("Getting contact {ContactId} for user {UserId}", id, userId);
    
    var contact = await _repository.GetByIdAsync(userId, id);
    if (contact is null)
        return Result<ContactDto>.Failure(DomainErrors.Contact.NotFound);
    
    return Result<ContactDto>.Success(MapToDto(contact));
}
```

## Error Handling
- Use `Result<T>` pattern from `ACI.Application.Common`
- Define errors in `DomainErrors.cs`
- Controllers use `ResultExtensions.ToActionResult()`
- Global exception handler catches unhandled exceptions

## Logging
- Inject `ILogger<T>` in constructors
- Log at appropriate levels:
  - `Information`: Normal operations (CRUD success)
  - `Warning`: Recoverable issues (validation failures, not found)
  - `Error`: Exceptions and failures
- Include context: `{EntityId}`, `{UserId}`, `{OrganizationId}`

## Validation
- Use DataAnnotations on DTOs: `[Required]`, `[EmailAddress]`, `[StringLength]`
- Use `ValidationHelper` for format validation
- Validate in service layer before database operations

## Repository Pattern
- Always filter by `UserId` and `OrganizationId`
- Use `FilterByUserAndOrg(userId, organizationId)` helper
- Never expose entities directly to controllers

## Testing
- Unit tests in `ACI.Application.Tests`
- Use Moq for mocking dependencies
- Use FluentAssertions for assertions
- Test both success and failure paths
