using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Api.Requests;

public class ConfirmEmailRequest
{
    [Required] public required string UserId { get; init; }
    [Required] public required string Token { get; init; }
}
