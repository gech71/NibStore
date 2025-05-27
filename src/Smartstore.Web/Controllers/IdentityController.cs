using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Smartstore.ComponentModel;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common.Configuration;

using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Localization.Routing;
using Smartstore.Core.Logging;
using Smartstore.Core.Messaging;
using Smartstore.Core.Platform.Identity.Services;
using Smartstore.Core.Security;
using Smartstore.Core.Seo.Routing;
using Smartstore.Core.Stores;
using Smartstore.Core.Web;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Models.Customers;
using Smartstore.Web.Models.Identity;
using Smartstore.Web.Rendering;
using Smartstore.Core.Common.Services;
namespace Smartstore.Web.Controllers
{
    public class IdentityController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly UserManager<Customer> _userManager;
        private readonly SignInManager<Customer> _signInManager;
        private readonly RoleManager<CustomerRole> _roleManager;
        private readonly IProviderManager _providerManager;
        private readonly ITaxService _taxService;
        private readonly IAddressService _addressService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IMessageFactory _messageFactory;
        private readonly IWebHelper _webHelper;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly CustomerSettings _customerSettings;
        private readonly CaptchaSettings _captchaSettings;
        private readonly DateTimeSettings _dateTimeSettings;
        private readonly TaxSettings _taxSettings;
        private readonly LocalizationSettings _localizationSettings;
        private readonly ExternalAuthenticationSettings _externalAuthenticationSettings;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private  readonly IUserPhoneStore _userPhoneStore;
        private readonly IOtpService _otpService;
        public IdentityController(
       SmartDbContext db,
       UserManager<Customer> userManager,
       SignInManager<Customer> signInManager,
       RoleManager<CustomerRole> roleManager,
       IProviderManager providerManager,
       ITaxService taxService,
       IAddressService addressService,
       IShoppingCartService shoppingCartService,
       IMessageFactory messageFactory,
       IWebHelper webHelper,
       IDateTimeHelper dateTimeHelper,
       CustomerSettings customerSettings,
       CaptchaSettings captchaSettings,
       DateTimeSettings dateTimeSettings,
       TaxSettings taxSettings,
       LocalizationSettings localizationSettings,
       ExternalAuthenticationSettings externalAuthenticationSettings,
       RewardPointsSettings rewardPointsSettings,
       IUserPhoneStore userPhoneStore,
       IOtpService otpService)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _providerManager = providerManager;
            _taxService = taxService;
            _addressService = addressService;
            _shoppingCartService = shoppingCartService;
            _messageFactory = messageFactory;
            _webHelper = webHelper;
            _dateTimeHelper = dateTimeHelper;
            _customerSettings = customerSettings;
            _captchaSettings = captchaSettings;
            _dateTimeSettings = dateTimeSettings;
            _taxSettings = taxSettings;
            _localizationSettings = localizationSettings;
            _externalAuthenticationSettings = externalAuthenticationSettings;
            _rewardPointsSettings = rewardPointsSettings;
            _userPhoneStore = userPhoneStore;
            _otpService = otpService;
        }

        #region Login / Logout / Register

        [HttpGet]
        [TypeFilter(typeof(DisplayExternalAuthWidgets))]
        [AllowAnonymous, NeverAuthorize, CheckStoreClosed(false)]
        [LocalizedRoute("/login", Name = "Login")]
        public IActionResult Login(bool? checkoutAsGuest, string returnUrl = null)
        {
            var model = new LoginModel
            {
                CheckoutAsGuest = checkoutAsGuest.GetValueOrDefault()
            };

            ViewBag.ReturnUrl = returnUrl ?? Url.Content("~/");

            return View(model);
        }

        [HttpPost]
        [TypeFilter(typeof(DisplayExternalAuthWidgets))]
        [AllowAnonymous, NeverAuthorize]
        [ValidateCaptcha(CaptchaSettingName = nameof(CaptchaSettings.ShowOnLoginPage))]
        [ValidateAntiForgeryToken, CheckStoreClosed(false)]
        [LocalizedRoute("/login", Name = "Login")]
        public async Task<IActionResult> Login(LoginModel model, string returnUrl, string captchaError)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var phoneNumber = model.PhoneNumber?.Trim();

            var customer = await _userPhoneStore.FindByPhoneNumberAsync(phoneNumber, CancellationToken.None);

            if (customer == null)
            {
                ModelState.AddModelError(string.Empty, T("Account.Login.WrongCredentials"));
                return View(model);
            }
            //tade
            // ✅ Validate OTP
            if (!_otpService.ValidateOtp(phoneNumber, model.OtpCode))
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP.");
                return View(model);
            }

            await _signInManager.SignInAsync(customer, model.RememberMe);

            await Services.EventPublisher.PublishAsync(new CustomerSignedInEvent { Customer = customer });
            await _shoppingCartService.MigrateCartAsync(Services.WorkContext.CurrentCustomer, customer);
            Services.ActivityLogger.LogActivity(KnownActivityLogTypes.PublicStoreLogin, T("ActivityLog.PublicStore.Login"), customer);
      
            if (string.IsNullOrEmpty(returnUrl) ||
                returnUrl == "/" ||
                returnUrl.Contains("/passwordrecovery", StringComparison.OrdinalIgnoreCase) ||
                returnUrl.Contains("/activation", StringComparison.OrdinalIgnoreCase) ||
                !Url.IsLocalUrl(returnUrl))
            {
                return RedirectToRoute("Homepage");
            }

            return Redirect(returnUrl);
        }


        [NeverAuthorize, CheckStoreClosed(false)]
        [DisallowRobot(true)]
        [LocalizedRoute("/logout", Name = "Logout")]
        public async Task<IActionResult> Logout()
        {
            var workContext = Services.WorkContext;
            var db = Services.DbContext;

            if (workContext.CurrentImpersonator != null)
            {
                // Logout impersonated customer.
                workContext.CurrentImpersonator.GenericAttributes.ImpersonatedCustomerId = null;
                await db.SaveChangesAsync();

                // Redirect back to customer details page (admin area).
                return RedirectToAction("Edit", "Customer", new { id = workContext.CurrentCustomer.Id, area = "Admin" });
            }
            else
            {
                // Standard logout
                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.PublicStoreLogout, T("ActivityLog.PublicStore.Logout"));

                await _signInManager.SignOutAsync();
                await db.SaveChangesAsync();

                return RedirectToRoute("Homepage");
            }
        }

        [HttpGet]
        [AllowAnonymous, NeverAuthorize]
        [LocalizedRoute("/register", Name = "Register")]
        public async Task<IActionResult> Register(string returnUrl = null)
        {
            // Check whether registration is allowed.
            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
            {
                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.Disabled });
            }

            ViewBag.ReturnUrl = returnUrl;

            var model = new RegisterModel();
            await PrepareRegisterModelAsync(model);

            return View(model);
        }
       [HttpPost]
        [AllowAnonymous, NeverAuthorize]
        [ValidateCaptcha(CaptchaSettingName = nameof(CaptchaSettings.ShowOnRegistrationPage))]
        [ValidateAntiForgeryToken, ValidateHoneypot]
        [LocalizedRoute("/register", Name = "Register")]
        public async Task<IActionResult> Register(RegisterModel model, string captchaError, string returnUrl = null)
        {
            // Check whether registration is allowed
            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
            {
                return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.Disabled });
            }

            var customer = Services.WorkContext.CurrentCustomer;

            if (customer.IsRegistered())
            {
                return RedirectToRoute("RegisterResult", new { message = T("Account.Register.Result.AlreadyRegistered").Value });
            }

            if (_captchaSettings.ShowOnRegistrationPage && captchaError.HasValue())
            {
                ModelState.AddModelError(string.Empty, captchaError);
            }

            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                await PrepareRegisterModelAsync(model);
                return View(model);
            }
            //tade
                 // ✅ OTP Validation block added here
            if (!_otpService.ValidateOtp(model.Phone, model.OtpCode))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Invalid or expired OTP.");
                await PrepareRegisterModelAsync(model);
                return View(model);
            }

            var succeeded = false;
            var phone = model.Phone?.Trim();
            var username = model.Username?.Trim();

            // Generate fallback email
            // Extract only digits from the phone number
            var digits = new string(phone?.Where(char.IsDigit).ToArray());

            // Take last 9 digits, or less if not available
            var shortPhone = digits.Length >= 9 ? digits[^9..] : digits;

            var fallbackEmail = $"user_{shortPhone}@local.app";

            try
            {
                // Set basic properties
                customer.Username = username;
                customer.Email = fallbackEmail; // Replace later if needed
                customer.PasswordFormat = _customerSettings.DefaultPasswordFormat;
                customer.Active = _customerSettings.UserRegistrationType == UserRegistrationType.Standard;
                customer.CreatedOnUtc = DateTime.UtcNow;
                customer.LastActivityDateUtc = DateTime.UtcNow;

                // Update customer info
                var identityResult = await _userManager.UpdateAsync(customer);
                if (identityResult.Succeeded)
                {
                    succeeded = identityResult.Succeeded;
                }

                AddModelStateErrors(identityResult);

                if (succeeded)
                {
                    // Map additional fields from model to customer
                    await MapRegisterModelToCustomerAsync(customer, model);

                    // Finalize registration
                    return await FinalizeCustomerRegistrationAsync(customer, returnUrl);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ModelState.AddModelError(string.Empty, T("Common.Error.Unspecified"));
            }

            // Something failed, restore previous state and redisplay form
            await PrepareRegisterModelAsync(model);
            return View(model);
        }
        [HttpGet]
        [AllowAnonymous, NeverAuthorize]
        [LocalizedRoute("/registerresult", Name = "RegisterResult")]
        public IActionResult RegisterResult(int? resultId, string message = "")
        {
            var resultText = string.Empty;

            if (resultId != null)
            {
                switch ((UserRegistrationType)resultId)
                {
                    case UserRegistrationType.Disabled:
                        resultText = T("Account.Register.Result.Disabled");
                        break;
                    case UserRegistrationType.Standard:
                        resultText = T("Account.Register.Result.Standard");
                        break;
                    case UserRegistrationType.AdminApproval:
                        resultText = T("Account.Register.Result.AdminApproval");
                        break;
                    case UserRegistrationType.EmailValidation:
                        resultText = T("Account.Register.Result.EmailValidation");
                        break;
                    default:
                        break;
                }
            }

            ViewBag.RegisterResult = resultText.HasValue() ? resultText + " " + message : message;
            return View();
        }

        [HttpGet]
        [AllowAnonymous, NeverAuthorize]
        [LocalizedRoute("/customer/activation", Name = "AccountActivation")]
        public async Task<IActionResult> AccountActivation(string token, string email)
        {
            var customer = await _userManager.FindByEmailAsync(email);

            if (customer == null)
            {
                NotifyError(T("Account.AccountActivation.InvalidEmailOrToken"));
                return RedirectToRoute("Homepage");
            }

            // Validate token & set user to active.
            var confirmed = await _userManager.ConfirmEmailAsync(customer, token);

            if (!confirmed.Succeeded)
            {
                NotifyError(T("Account.AccountActivation.InvalidEmailOrToken"));
                return RedirectToRoute("HomePage");
            }

            // If token wasn't proved invalid by ConfirmEmailAsync() a few lines above, Customer.Active was set to true & AccountActivationToken was resetted in UserStore.
            // So we better save here.
            await _db.SaveChangesAsync();

            // Send welcome message.
            await _messageFactory.SendCustomerWelcomeMessageAsync(customer, Services.WorkContext.WorkingLanguage.Id);

            ViewBag.ActivationResult = T("Account.AccountActivation.Activated");

            return View();
        }

        #endregion

        #region Profile

        [DisallowRobot]
        [LocalizedRoute("/profile/{id:int}", Name = "CustomerProfile")]
        public async Task<IActionResult> CustomerProfile(int id)
        {
            if (!_customerSettings.AllowViewingProfiles)
            {
                return NotFound();
            }

            var customer = await _db.Customers
                .IncludeCustomerRoles()
                .FindByIdAsync(id, false);

            // Guests do not have a customer profile.
            if (customer?.IsGuest() ?? true)
            {
                return NotFound();
            }

            var info = new ProfileInfoModel
            {
                Id = customer.Id,
                Avatar = await customer.MapAsync(null, true)
            };

            // Location.
            if (_customerSettings.ShowCustomersLocation)
            {
                var country = await _db.Countries.FindByIdAsync(customer.GenericAttributes.CountryId ?? 0, false);

                info.LocationEnabled = country != null;
                info.Location = country?.GetLocalized(x => x.Name);
            }

            // Registration date.
            if (_customerSettings.ShowCustomersJoinDate)
            {
                info.JoinDateEnabled = true;
                info.JoinDate = Services.DateTimeHelper.ConvertToUserTime(customer.CreatedOnUtc, DateTimeKind.Utc).ToString("f");
            }

            // Birth date.
            if (_customerSettings.DateOfBirthEnabled && customer.BirthDate.HasValue)
            {
                info.DateOfBirthEnabled = true;
                info.DateOfBirth = customer.BirthDate.Value.ToString("D");
            }

            var model = new ProfileIndexModel
            {
                Id = customer.Id,
                CustomerName = customer.FormatUserName(_customerSettings, T, true),
                ProfileInfo = info
            };

            return View(model);
        }

        #endregion

        #region Change password

        [DisallowRobot]
        public IActionResult ChangePassword()
        {
            if (!Services.WorkContext.CurrentCustomer.IsRegistered())
            {
                return ChallengeOrForbid();
            }

            return View(new ChangePasswordModel());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            var customer = Services.WorkContext.CurrentCustomer;
            if (!customer.IsRegistered())
            {
                return ChallengeOrForbid();
            }

            if (ModelState.IsValid)
            {
                var passwordResult = await _userManager.ChangePasswordAsync(customer, model.OldPassword, model.NewPassword);
                if (passwordResult.Succeeded)
                {
                    model.Result = T("Account.ChangePassword.Success");
                }

                AddModelStateErrors(passwordResult);
            }

            return View(model);
        }

        #endregion

        #region Password recovery

        [DisallowRobot]
        [LocalizedRoute("/passwordrecovery", Name = "PasswordRecovery")]
        public IActionResult PasswordRecovery()
        {
            var model = new PasswordRecoveryModel
            {
                DisplayCaptcha = _captchaSettings.CanDisplayCaptcha && _captchaSettings.ShowOnPasswordRecoveryPage
            };

            return View(model);
        }

        [HttpPost, DisallowRobot]
        [ValidateCaptcha(CaptchaSettingName = nameof(CaptchaSettings.ShowOnPasswordRecoveryPage))]
        [LocalizedRoute("/passwordrecovery", Name = "PasswordRecovery")]
        [FormValueRequired("send-email")]
        public async Task<IActionResult> PasswordRecovery(PasswordRecoveryModel model, string captchaError)
        {
            if (_captchaSettings.ShowOnPasswordRecoveryPage && captchaError.HasValue())
            {
                ModelState.AddModelError(string.Empty, captchaError);
            }

            if (ModelState.IsValid)
            {
                var customer = await _userManager.FindByEmailAsync(model.Email);
                if (customer != null && customer.Active)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(customer);
                    customer.GenericAttributes.PasswordRecoveryToken = token;
                    await _db.SaveChangesAsync();

                    await _messageFactory.SendCustomerPasswordRecoveryMessageAsync(customer, Services.WorkContext.WorkingLanguage.Id);
                    model.ResultState = PasswordRecoveryResultState.Success;
                }
                else
                {
                    model.ResultState = PasswordRecoveryResultState.Error;
                }

                model.ResultMessage = T("Account.PasswordRecovery.EmailHasBeenSent");

                return View(model);
            }

            // If we got this far something failed. Redisplay form.
            model.DisplayCaptcha = _captchaSettings.CanDisplayCaptcha && _captchaSettings.ShowOnPasswordRecoveryPage;

            return View(model);
        }

        public IActionResult PasswordRecoveryConfirm(string token, string email)
        {
            var model = new PasswordRecoveryConfirmModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost, ActionName("PasswordRecoveryConfirm")]
        [FormValueRequired("set-password")]
        public async Task<IActionResult> PasswordRecoveryConfirmPOST(PasswordRecoveryConfirmModel model)
        {
            var customer = await _userManager.FindByEmailAsync(model.Email);

            if (ModelState.IsValid)
            {
                var identityResult = await _userManager.ResetPasswordAsync(customer, model.Token, model.NewPassword);
                if (identityResult.Succeeded)
                {
                    customer.GenericAttributes.PasswordRecoveryToken = string.Empty;
                    // Detect and repair accidental guest role assignments
                    await CheckRegisteredRole(customer);
                    await _db.SaveChangesAsync();

                    model.SuccessfullyChanged = true;
                    model.Result = T("Account.PasswordRecovery.PasswordHasBeenChanged");
                }
                else
                {
                    identityResult.Errors.Each(x => NotifyError(x.Description));

                    return RedirectToAction(nameof(PasswordRecoveryConfirm), new { token = model.Token, email = model.Email });
                }

                return View(model);
            }

            // If we got this far something failed. Redisplay form.
            return View(model);
        }

        #endregion

        #region External login

        [HttpGet]
        [AllowAnonymous, DisallowRobot]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Identity");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            properties.AllowRefresh = true;
            properties.IsPersistent = true;

            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                NotifyError(remoteError);
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                // INFO: if you get here and wonder why it fails, call HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme)
                // (which is internally called by GetExternalLoginInfoAsync) and check AuthenticateResult for errors.
                return RedirectToAction(nameof(Login));
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (result.Succeeded)
            {
                var customer = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (customer != null)
                {
                    await FinalizeLoginAsync(Services.WorkContext.CurrentCustomer, customer, logActivity: false);
                }

                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.PublicStoreLogin, T("ActivityLog.PublicStore.LoginExternal"), info.LoginProvider);
                return RedirectToReferrer(returnUrl, () => RedirectToRoute("Homepage"));
            }
            else
            {
                // User doesn't have an account yet.
                if (_customerSettings.UserRegistrationType != UserRegistrationType.Disabled)
                {
                    var customer = new Customer
                    {
                        Username = info.Principal.FindFirstValue(ClaimTypes.Name),
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email),
                        PasswordFormat = _customerSettings.DefaultPasswordFormat,
                        Active = _customerSettings.UserRegistrationType == UserRegistrationType.Standard,
                        CreatedOnUtc = DateTime.UtcNow,
                        LastActivityDateUtc = DateTime.UtcNow
                    };

                    var identityResult = await _userManager.CreateAsync(customer);
                    if (identityResult.Succeeded)
                    {
                        // INFO: this creates the external auth record.
                        identityResult = await _userManager.AddLoginAsync(customer, info);
                        if (identityResult.Succeeded)
                        {
                            return await FinalizeCustomerRegistrationAsync(customer, returnUrl);
                        }

                        await FinalizeLoginAsync(Services.WorkContext.CurrentCustomer, customer, logActivity: true);
                    }

                    // Display errors to user.
                    identityResult.Errors.Each(x => NotifyError(x.Description));
                }
                else
                {
                    // Creating new accounts is disabled.
                    NotifyError(T("Account.Register.Result.Disabled"));
                }

                return RedirectToLocal(returnUrl);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ExternalErrorCallback(string provider, string errorMessage)
        {
            if (provider.HasValue() || errorMessage.HasValue())
            {
                Logger.Error($"Error from external provider {provider}: {errorMessage}");
            }

            NotifyError(T("ExternalAuthentication.ConfigError"));
            return RedirectToAction(nameof(Login));
        }

        #endregion

        #region Access

        [HttpGet]
        [AllowAnonymous, NeverAuthorize]
        [LocalizedRoute("/access-denied", Name = "AccessDenied")]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            throw new AccessDeniedException(null, returnUrl);
        }

        #endregion

        #region Helpers

        private async Task PrepareRegisterModelAsync(RegisterModel model)
        {

            MiniMapper.Map(_customerSettings, model);

            model.UsernamesEnabled = _customerSettings.CustomerLoginType != CustomerLoginType.Email;
            model.CheckUsernameAvailabilityEnabled = _customerSettings.CheckUsernameAvailabilityEnabled;

            ViewBag.AvailableTimeZones = _dateTimeHelper.GetSystemTimeZones()
                .ToSelectListItems(_dateTimeHelper.DefaultStoreTimeZone.Id);
        }

        /// <summary>
        /// Assigns customer roles, publishes an event, sends email messages, signs the customer in depending on configuration & returns appropriate redirect.
        /// </summary>
        private async Task<IActionResult> FinalizeCustomerRegistrationAsync(Customer customer, string returnUrl)
        {
            // Remove ClientIdent: no other "same-building" guest should be identified by this ident.
            customer.ClientIdent = null;

            await AssignCustomerRolesAsync(customer);

            // Add reward points for customer registration (if enabled).
            if (_rewardPointsSettings.Enabled && _rewardPointsSettings.PointsForRegistration > 0)
            {
                customer.AddRewardPointsHistoryEntry(_rewardPointsSettings.PointsForRegistration, T("RewardPoints.Message.RegisteredAsCustomer"));
            }

            await Services.EventPublisher.PublishAsync(new CustomerRegisteredEvent { Customer = customer });

            // Notifications
            if (_customerSettings.NotifyNewCustomerRegistration)
            {
                await _messageFactory.SendCustomerRegisteredNotificationMessageAsync(customer, _localizationSettings.DefaultAdminLanguageId);
            }

            switch (_customerSettings.UserRegistrationType)
            {
                case UserRegistrationType.EmailValidation:
                {
                    // Send an email with generated token.
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(customer);

                    customer.GenericAttributes.AccountActivationToken = code;
                    await _db.SaveChangesAsync();
                    await _messageFactory.SendCustomerEmailValidationMessageAsync(customer, Services.WorkContext.WorkingLanguage.Id);

                    return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.EmailValidation });
                }
                case UserRegistrationType.AdminApproval:
                {
                    return RedirectToRoute("RegisterResult", new { resultId = (int)UserRegistrationType.AdminApproval });
                }
                case UserRegistrationType.Standard:
                {
                    // Send customer welcome message.
                    await _messageFactory.SendCustomerWelcomeMessageAsync(customer, Services.WorkContext.WorkingLanguage.Id);
                    await _signInManager.SignInAsync(customer, isPersistent: false);

                    var redirectUrl = Url.RouteUrl("RegisterResult", new { resultId = (int)UserRegistrationType.Standard });
                    if (returnUrl.HasValue())
                    {
                        redirectUrl = _webHelper.ModifyQueryString(redirectUrl, "returnUrl=" + returnUrl.UrlEncode());
                    }

                    return Redirect(redirectUrl);
                }
                default:
                {
                    return RedirectToRoute("Homepage");
                }
            }
        }

        private async Task<bool> CheckRegisteredRole(Customer customer)
        {
            if (customer.IsSystemAccount || (customer.Username.IsEmpty() && customer.Email.IsEmpty()))
            {
                return false;
            }

            var updated = false;
            var guestRoleMappings = customer.CustomerRoleMappings
                .Where(x => !x.IsSystemMapping && x.CustomerRole.SystemName.EqualsNoCase(SystemCustomerRoleNames.Guests))
                .ToArray();
            if (guestRoleMappings.Length > 0)
            {
                _db.CustomerRoleMappings.RemoveRange(guestRoleMappings);
                updated = true;
            }

            if (!customer.IsRegistered())
            {               
                var registeredRole = await _db.CustomerRoles
                    .AsNoTracking()
                    .Where(x => x.SystemName == SystemCustomerRoleNames.Registered)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync();
                if (registeredRole != null)
                {
                    _db.CustomerRoleMappings.Add(new()
                    {
                        CustomerId = customer.Id,
                        CustomerRoleId = registeredRole.Id
                    });
                    updated = true;
                }
            }

            return updated;
        }

        private async Task MapRegisterModelToCustomerAsync(Customer customer, RegisterModel model)
        {
            // Set customer number if required and not already set
            if (_customerSettings.CustomerNumberMethod == CustomerNumberMethod.AutomaticallySet &&
                string.IsNullOrWhiteSpace(customer.CustomerNumber))
            {
                customer.CustomerNumber = customer.Id.ToString();
            }

            // Map phone number if enabled
            if (_customerSettings.PhoneEnabled)
            {
                customer.GenericAttributes.Phone = model.Phone?.Trim();
            }

            // Try to build a default address from available data
            var defaultAddress = new Address
            {
                Title = customer.Title,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Company = customer.Company,

                CountryId = customer.GenericAttributes.CountryId,
                StateProvinceId = customer.GenericAttributes.StateProvinceId,
                City = customer.GenericAttributes.City,
                ZipPostalCode = customer.GenericAttributes.ZipPostalCode,

                Address1 = customer.GenericAttributes.StreetAddress,
                Address2 = customer.GenericAttributes.StreetAddress2,

                PhoneNumber = customer.GenericAttributes.Phone,
                FaxNumber = customer.GenericAttributes.Fax,

                CreatedOnUtc = DateTime.UtcNow
            };

            // Validate the address before saving
            if (await _addressService.IsAddressValidAsync(defaultAddress))
            {
                // Add to addresses list
                customer.Addresses.Add(defaultAddress);

                // Set as default billing and shipping addresses
                customer.BillingAddress = defaultAddress;
                customer.ShippingAddress = defaultAddress;

                // Persist changes
                _db.TryUpdate(customer);
                await _db.SaveChangesAsync();
            }
        }

        private async Task AssignCustomerRolesAsync(Customer customer)
        {
            // Add customer to 'Registered' role.
            var registeredRole = await _db.CustomerRoles
                .Where(x => x.SystemName == SystemCustomerRoleNames.Registered)
                .FirstOrDefaultAsync();

            await _userManager.AddToRoleAsync(customer, registeredRole.Name);

            // Add customer to an additional role.
            var roleIdToAdd = _customerSettings.RegisterCustomerRoleId ?? 0;
            if (roleIdToAdd != 0 && roleIdToAdd != registeredRole.Id)
            {
                var customerRole = await _roleManager.FindByIdAsync(roleIdToAdd);
                if (customerRole != null)
                {
                    await _userManager.AddToRoleAsync(customer, customerRole.Name);
                }
            }

            // Remove customer from 'Guests' role.
            await _userManager.RemoveFromRoleAsync(customer, SystemCustomerRoleNames.Guests);
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            return RedirectToReferrer(returnUrl, () => RedirectToRoute("Login"));
        }

        private void AddModelStateErrors(IdentityResult result)
        {
            if (!result.Succeeded)
            {
                result.Errors.Select(x => x.Description).Distinct()
                    .Each(x => ModelState.AddModelError(string.Empty, x));
            }
        }

        private async Task FinalizeLoginAsync(Customer guest, Customer registered, bool logActivity) 
        {
            await _shoppingCartService.MigrateCartAsync(guest, registered);
            await Services.EventPublisher.PublishAsync(new CustomerSignedInEvent { Customer = registered });

            if (logActivity)
            {
                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.PublicStoreLogin, T("ActivityLog.PublicStore.Login"), registered);
            }
        }

        #endregion
    }
}