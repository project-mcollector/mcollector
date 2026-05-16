using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Application.Options;

public class MailOptions
{
    [Required]
    public string FrontendUrl { get; init; } = "";

    public string FromAddress { get; init; } = "MCollector <noreply@mail.mcollector.publicvm.com>";
}
