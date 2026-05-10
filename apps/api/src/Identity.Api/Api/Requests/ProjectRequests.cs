using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Api.Requests;

public class CreateProjectRequest
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }
}

public class AddMemberRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; init; }
}
