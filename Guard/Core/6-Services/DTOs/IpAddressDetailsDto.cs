using Guard.Core.Enums;

namespace Guard.Core.Services.DTOs;

public record IpAddressFieldsDto
{
  public string Address { get; set; } = "";
  public string? Description { get; set; }
  public Guid? SubdivisionId { get; set; }
}

public sealed record IpAddressDetailsDto : IpAddressFieldsDto
{
  public Guid Id { get; init; }
  public Status Status { get; init; } = Status.Inserted;
  public string? SubdivisionName { get; init; }
  public EntityInfoDto Info { get; init; } = new();
}

public sealed record EntityInfoDto
{
  public Guid Id { get; init; }
  public DateTime InsertedDate { get; init; }
  public DateTime? LastModifiedDate { get; init; }
  public string? Creator { get; init; }
  public string? Editor { get; init; }
}

public sealed record IpSubdivisionOptionDto(Guid Id, string Name);
public sealed record CreateIpAddressDto(string Address, string? Description, Guid? SubdivisionId);
public sealed record EditIpAddressDto(Guid Id, string Address, string? Description, Guid? SubdivisionId);
