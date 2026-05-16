using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Api.Requests;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ValidEmailAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailAttr = new();
    private static readonly MaxLengthAttribute MaxLengthAttr = new(256);

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var emailResult = EmailAttr.GetValidationResult(value, ctx);
        return emailResult != ValidationResult.Success
            ? emailResult
            : MaxLengthAttr.GetValidationResult(value, ctx);
    }
}
