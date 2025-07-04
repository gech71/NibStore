using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.ComponentModel;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Localization.Routing;
using Smartstore.Core.Logging;
using Smartstore.Core.Messaging;
using Smartstore.Core.Security;
using Smartstore.Core.Seo.Routing;
using Smartstore.Core.Stores;
using Smartstore.Core.Web;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Models.Customers;
using Smartstore.Web.Models.Identity;
using Smartstore.Web.Rendering;

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
            RewardPointsSettings rewardPointsSettings
        )
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
                // CustomerLoginType = _customerSettings.CustomerLoginType,
                CheckoutAsGuest = checkoutAsGuest.GetValueOrDefault(),
                // DisplayCaptcha = _captchaSettings.CanDisplayCaptcha && _captchaSettings.ShowOnLoginPage,
                // ShowPasswordField = false
            };

            ViewBag.ReturnUrl = returnUrl ?? Url.Content("~/");

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous, NeverAuthorize]
        [ValidateAntiForgeryToken, CheckStoreClosed(false)]
        [LocalizedRoute("/login", Name = "Login")]
        public async Task<IActionResult> Login(LoginModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            var phone = model.Phone?.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                ModelState.AddModelError(nameof(model.Phone), "Phone number is required.");
                return View(model);
            }

            var customer = await _userManager.Users.FirstOrDefaultAsync(c => c.Phone == phone);

            if (customer == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The phone number you entered is not registered."
                );
                return View(model);
            }

            var roles = await _db
                .CustomerRoleMappings.Where(m => m.CustomerId == customer.Id)
                .Select(m => m.CustomerRole.SystemName)
                .ToListAsync();

            if (string.IsNullOrEmpty(model.Password))
            {
                if (roles.Contains("Administrators") || roles.Contains("Merchant"))
                {
                    model.ShowPassword = true;
                    return View(model);
                }
                else if (roles.Count == 1 && roles.Contains("Registered"))
                {
                    await _signInManager.SignInAsync(customer, isPersistent: false);
                    return RedirectToRoute("Homepage");
                }
                else
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "You do not have permission to access this area."
                    );
                    return View(model);
                }
            }

            // if (string.IsNullOrWhiteSpace(model.Password))
            // {
            //     ModelState.AddModelError(nameof(model.Password), "Password is required.");
            //     model.ShowPassword = true;
            //     return View(model);
            // }

            if (roles.Contains("Administrators") || roles.Contains("Merchant"))
            {
                var result = await _signInManager.PasswordSignInAsync(
                    customer,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false
                );

                if (result.Succeeded)
                {
                    await Services.EventPublisher.PublishAsync(
                        new CustomerSignedInEvent { Customer = customer }
                    );
                    await _shoppingCartService.MigrateCartAsync(
                        Services.WorkContext.CurrentCustomer,
                        customer
                    );
                    Services.ActivityLogger.LogActivity(
                        KnownActivityLogTypes.PublicStoreLogin,
                        T("ActivityLog.PublicStore.Login"),
                        customer
                    );

                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Incorrect password. Please try again.");
                    model.ShowPassword = true;
                }
            }
            else
            {
                ModelState.AddModelError(
                    string.Empty,
                    "You are not authorized to log in with this account."
                );
            }

            return View(model);
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
                return RedirectToAction(
                    "Edit",
                    "Customer",
                    new { id = workContext.CurrentCustomer.Id, area = "Admin" }
                );
            }
            else
            {
                // Standard logout
                Services.ActivityLogger.LogActivity(
                    KnownActivityLogTypes.PublicStoreLogout,
                    T("ActivityLog.PublicStore.Logout")
                );

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
                return RedirectToRoute(
                    "RegisterResult",
                    new { resultId = (int)UserRegistrationType.Disabled }
                );
            }

            ViewBag.ReturnUrl = returnUrl;

            var model = new RegisterModel();
            await PrepareRegisterModelAsync(model);

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous, NeverAuthorize]
        // [ValidateCaptcha(CaptchaSettingName = nameof(CaptchaSettings.ShowOnRegistrationPage))]
        [ValidateAntiForgeryToken, ValidateHoneypot]
        [LocalizedRoute("/register", Name = "Register")]
        public async Task<IActionResult> Register(
            RegisterModel model,
            string captchaError,
            string returnUrl = null
        )
        {
            // Check whether registration is allowed.
            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
            {
                return RedirectToRoute(
                    "RegisterResult",
                    new { resultId = (int)UserRegistrationType.Disabled }
                );
            }

            var customer = Services.WorkContext.CurrentCustomer;
            if (customer.IsRegistered())
            {
                // Already registered customer.
                // await _signInManager.SignOutAsync();

                return RedirectToRoute(
                    "RegisterResult",
                    new { message = T("Account.Register.Result.AlreadyRegistered").Value }
                );
            }

            // if (_captchaSettings.ShowOnRegistrationPage && captchaError.HasValue())
            // {
            //     ModelState.AddModelError(string.Empty, captchaError);
            // }

            var tempPassword = "Temp@" + Guid.NewGuid().ToString("N").Substring(0, 8) + "A";

            foreach (var validator in _userManager.PasswordValidators)
            {
                AddModelStateErrors(
                    await validator.ValidateAsync(_userManager, customer, tempPassword)
                );
            }

            ViewData["ReturnUrl"] = returnUrl;

            var existingPhoneUser = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.Phone == model.Phone.Trim()
            );

            if (existingPhoneUser != null)
            {
                ModelState.AddModelError(string.Empty, "Phone already registered");
            }

            if (ModelState.IsValid)
            {
                var succeeded = false;
                var oldUserName = customer.Username;
                var oldEmail = customer.Email;
                var oldPasswordFormat = customer.PasswordFormat;
                var oldActive = customer.Active;
                var oldCreatedOn = customer.CreatedOnUtc;
                var oldLastActivityDate = customer.LastActivityDateUtc;

                customer.Username = model.Username;
                customer.Email = "user" + model.Phone + "@mystore.com";
                customer.Phone = model.Phone?.Trim();
                customer.PasswordFormat = _customerSettings.DefaultPasswordFormat;
                customer.Active =
                    _customerSettings.UserRegistrationType == UserRegistrationType.Standard;
                customer.CreatedOnUtc = DateTime.UtcNow;
                customer.LastActivityDateUtc = DateTime.UtcNow;

                try
                {
                    var identityResult = await _userManager.UpdateAsync(customer);
                    if (identityResult.Succeeded)
                    {
                        var passwordResult = await _userManager.AddPasswordAsync(
                            customer,
                            tempPassword
                        );
                        succeeded = passwordResult.Succeeded;
                        AddModelStateErrors(passwordResult);
                    }

                    AddModelStateErrors(identityResult);
                }
                finally
                {
                    if (!succeeded)
                    {
                        customer.Username = oldUserName;
                        customer.Email = oldEmail;
                        customer.PasswordFormat = oldPasswordFormat;
                        customer.Active = oldActive;
                        customer.CreatedOnUtc = oldCreatedOn;
                        customer.LastActivityDateUtc = oldLastActivityDate;

                        await _db.SaveChangesAsync();
                    }
                }

                if (succeeded)
                {
                    await MapRegisterModelToCustomerAsync(customer, model);

                    return await FinalizeCustomerRegistrationAsync(customer, returnUrl);
                }
            }

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
            await _messageFactory.SendCustomerWelcomeMessageAsync(
                customer,
                Services.WorkContext.WorkingLanguage.Id
            );

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

            var customer = await _db.Customers.IncludeCustomerRoles().FindByIdAsync(id, false);

            // Guests do not have a customer profile.
            if (customer?.IsGuest() ?? true)
            {
                return NotFound();
            }

            var info = new ProfileInfoModel
            {
                Id = customer.Id,
                Avatar = await customer.MapAsync(null, true),
            };

            // Location.
            if (_customerSettings.ShowCustomersLocation)
            {
                var country = await _db.Countries.FindByIdAsync(
                    customer.GenericAttributes.CountryId ?? 0,
                    false
                );

                info.LocationEnabled = country != null;
                info.Location = country?.GetLocalized(x => x.Name);
            }

            // Registration date.
            if (_customerSettings.ShowCustomersJoinDate)
            {
                info.JoinDateEnabled = true;
                info.JoinDate = Services
                    .DateTimeHelper.ConvertToUserTime(customer.CreatedOnUtc, DateTimeKind.Utc)
                    .ToString("f");
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
                ProfileInfo = info,
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
                var passwordResult = await _userManager.ChangePasswordAsync(
                    customer,
                    model.OldPassword,
                    model.NewPassword
                );
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
                DisplayCaptcha =
                    _captchaSettings.CanDisplayCaptcha
                    && _captchaSettings.ShowOnPasswordRecoveryPage,
            };

            return View(model);
        }

        [HttpPost, DisallowRobot]
        [ValidateCaptcha(CaptchaSettingName = nameof(CaptchaSettings.ShowOnPasswordRecoveryPage))]
        [LocalizedRoute("/passwordrecovery", Name = "PasswordRecovery")]
        [FormValueRequired("send-email")]
        public async Task<IActionResult> PasswordRecovery(
            PasswordRecoveryModel model,
            string captchaError
        )
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

                    await _messageFactory.SendCustomerPasswordRecoveryMessageAsync(
                        customer,
                        Services.WorkContext.WorkingLanguage.Id
                    );
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
            model.DisplayCaptcha =
                _captchaSettings.CanDisplayCaptcha && _captchaSettings.ShowOnPasswordRecoveryPage;

            return View(model);
        }

        public IActionResult PasswordRecoveryConfirm(string token, string email)
        {
            var model = new PasswordRecoveryConfirmModel { Email = email, Token = token };

            return View(model);
        }

        [HttpPost, ActionName("PasswordRecoveryConfirm")]
        [FormValueRequired("set-password")]
        public async Task<IActionResult> PasswordRecoveryConfirmPOST(
            PasswordRecoveryConfirmModel model
        )
        {
            var customer = await _userManager.FindByEmailAsync(model.Email);

            if (ModelState.IsValid)
            {
                var identityResult = await _userManager.ResetPasswordAsync(
                    customer,
                    model.Token,
                    model.NewPassword
                );
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

                    return RedirectToAction(
                        nameof(PasswordRecoveryConfirm),
                        new { token = model.Token, email = model.Email }
                    );
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
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                provider,
                redirectUrl
            );
            properties.AllowRefresh = true;
            properties.IsPersistent = true;

            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(
            string returnUrl = null,
            string remoteError = null
        )
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
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                false
            );
            if (result.Succeeded)
            {
                var customer = await _userManager.FindByLoginAsync(
                    info.LoginProvider,
                    info.ProviderKey
                );
                if (customer != null)
                {
                    await FinalizeLoginAsync(
                        Services.WorkContext.CurrentCustomer,
                        customer,
                        logActivity: false
                    );
                }

                Services.ActivityLogger.LogActivity(
                    KnownActivityLogTypes.PublicStoreLogin,
                    T("ActivityLog.PublicStore.LoginExternal"),
                    info.LoginProvider
                );
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
                        Active =
                            _customerSettings.UserRegistrationType == UserRegistrationType.Standard,
                        CreatedOnUtc = DateTime.UtcNow,
                        LastActivityDateUtc = DateTime.UtcNow,
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

                        await FinalizeLoginAsync(
                            Services.WorkContext.CurrentCustomer,
                            customer,
                            logActivity: true
                        );
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
            model.AllowCustomersToSetTimeZone = _dateTimeSettings.AllowCustomersToSetTimeZone;
            model.DisplayVatNumber = _taxSettings.EuVatEnabled;
            model.VatRequired = _taxSettings.VatRequired;

            MiniMapper.Map(_customerSettings, model);

            model.UsernamesEnabled = _customerSettings.CustomerLoginType != CustomerLoginType.Email;
            model.CheckUsernameAvailabilityEnabled =
                _customerSettings.CheckUsernameAvailabilityEnabled;
            model.DisplayCaptcha =
                _captchaSettings.CanDisplayCaptcha && _captchaSettings.ShowOnRegistrationPage;

            ViewBag.AvailableTimeZones = _dateTimeHelper
                .GetSystemTimeZones()
                .ToSelectListItems(_dateTimeHelper.DefaultStoreTimeZone.Id);

            if (_customerSettings.CountryEnabled)
            {
                if (_customerSettings.StateProvinceEnabled)
                {
                    var stateProvinces = await _db.StateProvinces.GetStateProvincesByCountryIdAsync(
                        model.CountryId
                    );

                    ViewBag.AvailableStates =
                        stateProvinces.ToSelectListItems(model.StateProvinceId ?? 0)
                        ?? new List<SelectListItem>
                        {
                            new SelectListItem { Text = T("Address.OtherNonUS"), Value = "0" },
                        };
                }
            }
        }

        /// <summary>
        /// Assigns customer roles, publishes an event, sends email messages, signs the customer in depending on configuration & returns appropriate redirect.
        /// </summary>
        private async Task<IActionResult> FinalizeCustomerRegistrationAsync(
            Customer customer,
            string returnUrl
        )
        {
            // Remove ClientIdent: no other "same-building" guest should be identified by this ident.
            customer.ClientIdent = null;

            await AssignCustomerRolesAsync(customer);

            // Add reward points for customer registration (if enabled).
            if (_rewardPointsSettings.Enabled && _rewardPointsSettings.PointsForRegistration > 0)
            {
                customer.AddRewardPointsHistoryEntry(
                    _rewardPointsSettings.PointsForRegistration,
                    T("RewardPoints.Message.RegisteredAsCustomer")
                );
            }

            await Services.EventPublisher.PublishAsync(
                new CustomerRegisteredEvent { Customer = customer }
            );

            // Notifications
            if (_customerSettings.NotifyNewCustomerRegistration)
            {
                await _messageFactory.SendCustomerRegisteredNotificationMessageAsync(
                    customer,
                    _localizationSettings.DefaultAdminLanguageId
                );
            }

            switch (_customerSettings.UserRegistrationType)
            {
                case UserRegistrationType.EmailValidation:
                {
                    // Send an email with generated token.
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(customer);

                    customer.GenericAttributes.AccountActivationToken = code;
                    await _db.SaveChangesAsync();
                    await _messageFactory.SendCustomerEmailValidationMessageAsync(
                        customer,
                        Services.WorkContext.WorkingLanguage.Id
                    );

                    return RedirectToRoute(
                        "RegisterResult",
                        new { resultId = (int)UserRegistrationType.EmailValidation }
                    );
                }
                case UserRegistrationType.AdminApproval:
                {
                    return RedirectToRoute(
                        "RegisterResult",
                        new { resultId = (int)UserRegistrationType.AdminApproval }
                    );
                }
                case UserRegistrationType.Standard:
                {
                    // Send customer welcome message.
                    await _messageFactory.SendCustomerWelcomeMessageAsync(
                        customer,
                        Services.WorkContext.WorkingLanguage.Id
                    );
                    await _signInManager.SignInAsync(customer, isPersistent: false);

                    var redirectUrl = Url.RouteUrl(
                        "RegisterResult",
                        new { resultId = (int)UserRegistrationType.Standard }
                    );
                    if (returnUrl.HasValue())
                    {
                        redirectUrl = _webHelper.ModifyQueryString(
                            redirectUrl,
                            "returnUrl=" + returnUrl.UrlEncode()
                        );
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
            if (
                customer.IsSystemAccount
                || (customer.Username.IsEmpty() && customer.Email.IsEmpty())
            )
            {
                return false;
            }

            var updated = false;
            var guestRoleMappings = customer
                .CustomerRoleMappings.Where(x =>
                    !x.IsSystemMapping
                    && x.CustomerRole.SystemName.EqualsNoCase(SystemCustomerRoleNames.Guests)
                )
                .ToArray();
            if (guestRoleMappings.Length > 0)
            {
                _db.CustomerRoleMappings.RemoveRange(guestRoleMappings);
                updated = true;
            }

            if (!customer.IsRegistered())
            {
                var registeredRole = await _db
                    .CustomerRoles.AsNoTracking()
                    .Where(x => x.SystemName == SystemCustomerRoleNames.Registered)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync();
                if (registeredRole != null)
                {
                    _db.CustomerRoleMappings.Add(
                        new() { CustomerId = customer.Id, CustomerRoleId = registeredRole.Id }
                    );
                    updated = true;
                }
            }

            return updated;
        }

        private async Task MapRegisterModelToCustomerAsync(Customer customer, RegisterModel model)
        {
            // Properties
            if (_dateTimeSettings.AllowCustomersToSetTimeZone)
            {
                customer.TimeZoneId = model.TimeZoneId;
            }

            // VAT number
            if (_taxSettings.EuVatEnabled)
            {
                customer.GenericAttributes.VatNumber = model.VatNumber;

                var vatCheckResult = await _taxService.GetVatNumberStatusAsync(model.VatNumber);
                customer.VatNumberStatusId = (int)vatCheckResult.Status;

                // Send VAT number admin notification.
                if (model.VatNumber.HasValue() && _taxSettings.EuVatEmailAdminWhenNewVatSubmitted)
                {
                    await _messageFactory.SendNewVatSubmittedStoreOwnerNotificationAsync(
                        customer,
                        model.VatNumber,
                        vatCheckResult.Address,
                        _localizationSettings.DefaultAdminLanguageId
                    );
                }
            }

            // Form fields
            customer.FirstName = model.FirstName;
            customer.LastName = model.LastName;

            if (_customerSettings.CompanyEnabled)
            {
                customer.Company = model.Company;
            }

            if (_customerSettings.DateOfBirthEnabled && model.DateOfBirth.HasValue)
            {
                customer.BirthDate = model.DateOfBirth;
            }

            if (
                _customerSettings.CustomerNumberMethod == CustomerNumberMethod.AutomaticallySet
                && customer.CustomerNumber.IsEmpty()
            )
            {
                customer.CustomerNumber = customer.Id.Convert<string>();
            }
            if (_customerSettings.GenderEnabled)
            {
                customer.Gender = model.Gender;
            }
            if (_customerSettings.ZipPostalCodeEnabled)
            {
                customer.GenericAttributes.ZipPostalCode = model.ZipPostalCode;
            }
            if (_customerSettings.CountryEnabled)
            {
                customer.GenericAttributes.CountryId = model.CountryId;
            }
            if (_customerSettings.StreetAddressEnabled)
            {
                customer.GenericAttributes.StreetAddress = model.StreetAddress;
            }
            if (_customerSettings.StreetAddress2Enabled)
            {
                customer.GenericAttributes.StreetAddress2 = model.StreetAddress2;
            }
            if (_customerSettings.CityEnabled)
            {
                customer.GenericAttributes.City = model.City;
            }
            if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
            {
                customer.GenericAttributes.StateProvinceId = model.StateProvinceId;
            }
            if (_customerSettings.PhoneEnabled)
            {
                customer.Phone = model.Phone;
            }
            if (_customerSettings.FaxEnabled)
            {
                customer.GenericAttributes.Fax = model.Fax;
            }

            // Newsletter subscription
            if (_customerSettings.NewsletterEnabled && model.Newsletter)
            {
                var subscription = await _db
                    .NewsletterSubscriptions.ApplyMailAddressFilter(
                        customer.Email,
                        Services.StoreContext.CurrentStore.Id
                    )
                    .FirstOrDefaultAsync();

                if (subscription != null)
                {
                    subscription.Active = true;
                }
                else
                {
                    subscription = new NewsletterSubscription
                    {
                        NewsletterSubscriptionGuid = Guid.NewGuid(),
                        Email = customer.Email,
                        Active = true,
                        CreatedOnUtc = DateTime.UtcNow,
                        StoreId = Services.StoreContext.CurrentStore.Id,
                        WorkingLanguageId = Services.WorkContext.WorkingLanguage.Id,
                    };

                    _db.NewsletterSubscriptions.Add(subscription);
                }

                await _db.SaveChangesAsync();
            }

            // Insert default address (if possible).
            var defaultAddress = new Address
            {
                Title = customer.Title,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Company = customer.Company,
                CountryId = customer.GenericAttributes.CountryId,
                ZipPostalCode = customer.GenericAttributes.ZipPostalCode,
                StateProvinceId = customer.GenericAttributes.StateProvinceId,
                City = customer.GenericAttributes.City,
                Address1 = customer.GenericAttributes.StreetAddress,
                Address2 = customer.GenericAttributes.StreetAddress2,
                PhoneNumber = customer.Phone,
                FaxNumber = customer.GenericAttributes.Fax,
                CreatedOnUtc = customer.CreatedOnUtc,
            };

            if (await _addressService.IsAddressValidAsync(defaultAddress))
            {
                // Set default addresses.
                customer.Addresses.Add(defaultAddress);
                customer.BillingAddress = defaultAddress;
                customer.ShippingAddress = defaultAddress;
            }

            _db.TryUpdate(customer);
            await _db.SaveChangesAsync();
        }

        private async Task AssignCustomerRolesAsync(Customer customer)
        {
            // Add customer to 'Registered' role.
            var registeredRole = await _db
                .CustomerRoles.Where(x => x.SystemName == SystemCustomerRoleNames.Registered)
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
                result
                    .Errors.Select(x => x.Description)
                    .Distinct()
                    .Each(x => ModelState.AddModelError(string.Empty, x));
            }
        }

        private async Task FinalizeLoginAsync(Customer guest, Customer registered, bool logActivity)
        {
            await _shoppingCartService.MigrateCartAsync(guest, registered);
            await Services.EventPublisher.PublishAsync(
                new CustomerSignedInEvent { Customer = registered }
            );

            if (logActivity)
            {
                Services.ActivityLogger.LogActivity(
                    KnownActivityLogTypes.PublicStoreLogin,
                    T("ActivityLog.PublicStore.Login"),
                    registered
                );
            }
        }

        #endregion
    }
}
