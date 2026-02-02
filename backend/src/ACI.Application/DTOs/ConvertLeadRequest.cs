namespace ACI.Application.DTOs;

public record ConvertLeadRequest(bool CreateContact, bool CreateDeal, string? DealName, string? DealValue, string? DealStage);
