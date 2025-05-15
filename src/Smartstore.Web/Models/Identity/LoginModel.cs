using FluentValidation;
using Smartstore.Core.Identity;

namespace Smartstore.Web.Models.Identity
{
    [LocalizedDisplay("Account.Login.Fields.")]
    public partial class LoginModel : ModelBase
    {
        public bool CheckoutAsGuest { get; set; }

        [LocalizedDisplay("*PhoneNumber", Prompt = "*PhoneNumber")]
        public string PhoneNumber { get; set; }

        [LocalizedDisplay("*RememberMe")]
        public bool RememberMe { get; set; }
    }
    
    public class LoginValidator : SmartValidator<LoginModel>
    {
        public LoginValidator(CustomerSettings customerSettings)
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .WithMessage("Phone number is required.")
                .Matches(@"^(?:\+251|0)(?:9|7)\d{8}$")
                .WithMessage("Invalid phone number format. Example: +251912345678 or 0912345678");
        }
    }
}

/*
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Smartstore.Core.Identity;

namespace Smartstore.Web.Models.Identity
{
    [LocalizedDisplay("Account.Login.Fields.")]
    public partial class LoginModel : ModelBase
    {
        public bool CheckoutAsGuest { get; set; }

        public CustomerLoginType CustomerLoginType { get; set; }

        [DataType(DataType.EmailAddress)]
        [LocalizedDisplay("*Email", Prompt = "*Email")]
        public string Email { get; set; }

        [LocalizedDisplay("*UserName", Prompt = "*UserName")]
        public string Username { get; set; }

        [LocalizedDisplay("*UsernameOrEmail", Prompt = "*UsernameOrEmail")]
        public string UsernameOrEmail { get; set; }

        [DataType(DataType.Password)]
        [LocalizedDisplay("*Password", Prompt = "*Password")]
        public string Password { get; set; }

        [LocalizedDisplay("*RememberMe")]
        public bool RememberMe { get; set; }

        public bool DisplayCaptcha { get; set; }
    }

    public class LoginValidator : SmartValidator<LoginModel>
    {
        public LoginValidator(CustomerSettings customerSettings)
        {
            var loginType = customerSettings.CustomerLoginType;

            if (loginType == CustomerLoginType.Email)
            {
                RuleFor(x => x.Email).NotEmpty().EmailAddressStrict();
            }
            else if (loginType == CustomerLoginType.Username)
            {
                RuleFor(x => x.Username).NotEmpty();
            }
            else
            {
                RuleFor(x => x.UsernameOrEmail).NotEmpty();
            }

            RuleFor(x => x.Password).NotEmpty();
        }
    }
}
*/
