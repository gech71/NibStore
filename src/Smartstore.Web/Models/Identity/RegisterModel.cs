using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Smartstore.Core.Localization;

namespace Smartstore.Web.Models.Identity
{
    [LocalizedDisplay("Account.Fields.")]
    public partial class RegisterModel : ModelBase
    {
        public bool UsernamesEnabled { get; set; }

        [LocalizedDisplay("*Username")]
        public string Username { get; set; }
        public bool CheckUsernameAvailabilityEnabled { get; set; }
        public bool PhoneEnabled { get; set; }
        public bool PhoneRequired { get; set; }

        [LocalizedDisplay("*Phone")]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        [LocalizedDisplay("*Email")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        //tade
        [Required]
        public string OtpCode { get; set; }
    }

    public class RegisterModelValidator : SmartValidator<RegisterModel>
    {
        public RegisterModelValidator(Localizer T)
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required.")
                .MinimumLength(5).WithMessage("Username must be at least 5 characters long.")
                .Matches(@"^[A-Za-z][A-Za-z0-9_]*$")
                .WithMessage("Username must start with a letter and can only contain letters, numbers, and underscores.");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^(?:\+251|0)(?:9|7)\d{8}$")
                .WithMessage("Invalid phone number format.");
        }
    }
}