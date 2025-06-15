using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Smartstore.Core.Identity;

namespace Smartstore.Web.Models.Identity
{
    [LocalizedDisplay("Account.Login.Fields.")]
    public partial class LoginModel : ModelBase
    {
        public bool CheckoutAsGuest { get; set; }

        // public CustomerLoginType CustomerLoginType { get; set; }

        // [DataType(DataType.EmailAddress)]
        // [LocalizedDisplay("*Email", Prompt = "*Email")]
        // public string Email { get; set; }

        // [LocalizedDisplay("*UserName", Prompt = "*UserName")]
        // public string Username { get; set; }

        [LocalizedDisplay("*Phone", Prompt = "*PhoneNumber")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^(?:\+251|0)?9\d{8}$", ErrorMessage = "*Invalid phone number format")]
        [Required(ErrorMessage = "*Phone number is required.")]
        public string Phone { get; set; }

        // [LocalizedDisplay("*UsernameOrEmail", Prompt = "*UsernameOrEmail")]
        // public string UsernameOrEmail { get; set; }

        [DataType(DataType.Password)]
        [LocalizedDisplay("*Password", Prompt = "*Password")]
        public string Password { get; set; }

        [LocalizedDisplay("*RememberMe")]
        public bool RememberMe { get; set; }
        public bool DisplayCaptcha { get; set; }
        public bool ShowPassword { get; set; } = false;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ShowPassword && string.IsNullOrWhiteSpace(Password))
            {
                yield return new ValidationResult(
                    "Password is required.",
                    new[] { nameof(Password) }
                );
            }
        }
    }

    public class LoginValidator : SmartValidator<LoginModel>
    {
        public LoginValidator(CustomerSettings customerSettings)
        {
            // var loginType = customerSettings.CustomerLoginType;

            // if (loginType == CustomerLoginType.Email)
            // {
            //     RuleFor(x => x.Email).NotEmpty().EmailAddressStrict();
            // }
            // else if (loginType == CustomerLoginType.Username)
            // {
            //     RuleFor(x => x.Username).NotEmpty();
            // }
            // else
            // {
            //     RuleFor(x => x.UsernameOrEmail).NotEmpty();
            // }

            RuleFor(x => x.Phone)
                .NotEmpty()
                .WithMessage("Phone number is required.")
                .Matches(@"^(?:\+251|0)?9\d{8}$")
                .WithMessage(
                    "Phone number must be in the format 0912345678, +251912345678, or 912345678."
                );

            RuleFor(x => x.Password)
                .NotEmpty()
                .When(x => x.ShowPassword)
                .WithMessage("Password is required.");
        }
    }
}
