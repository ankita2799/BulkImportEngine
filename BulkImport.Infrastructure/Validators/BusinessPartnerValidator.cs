using BulkImport.Core.Models;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Infrastructure.Validators
{
    public class BusinessPartnerValidator : AbstractValidator<BusinessPartnerImportRow>
    {
        // Valid partner types — same as your lookup set check in current approach
        private static readonly string[] ValidTypes =
            { "Customer", "Supplier", "Both" };

        public BusinessPartnerValidator()
        {
            // Name
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

            // TaxId
            RuleFor(x => x.TaxId)
                .NotEmpty().WithMessage("TaxId is required.")
                .MaximumLength(50).WithMessage("TaxId cannot exceed 50 characters.")
                .Matches(@"^[A-Za-z0-9\-]+$")
                .WithMessage("TaxId can only contain letters, numbers and hyphens.");

            // Email — optional but must be valid format if provided
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email format is invalid.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));

            // Phone — optional but max length if provided
            RuleFor(x => x.Phone)
                .MaximumLength(30).WithMessage("Phone cannot exceed 30 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.Phone));

            // Type — must be from defined set, same as your repo lookup check
            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Type is required.")
                .Must(t => ValidTypes.Contains(t))
                .WithMessage($"Type must be one of: {string.Join(", ", ValidTypes)}");
        }
    }
}
