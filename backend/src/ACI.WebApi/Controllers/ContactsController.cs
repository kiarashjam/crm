using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly ICurrentUserService _currentUser;

    public ContactsController(IContactService contactService, ICurrentUserService currentUser)
    {
        _contactService = contactService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> GetContacts(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _contactService.GetContactsAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _contactService.SearchAsync(userId.Value, q ?? "", ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var contact = await _contactService.GetByIdAsync(id, userId.Value, ct);
        if (contact == null) return NotFound();
        return Ok(contact);
    }
}
