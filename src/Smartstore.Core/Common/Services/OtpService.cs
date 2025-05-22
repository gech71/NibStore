using Microsoft.Extensions.Caching.Memory;

namespace Smartstore.Core.Common.Services
{
    public interface IOtpService
    {
        Task<string?> SendOtpAsync(string phoneNumber);
        bool ValidateOtp(string phoneNumber, string otpCode);
    }

    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<string?> SendOtpAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return Task.FromResult<string?>(null);

            var otp = new Random().Next(100000, 999999).ToString();

            _cache.Set($"OTP_{phoneNumber}", otp, TimeSpan.FromMinutes(5));

            _logger.LogInformation($"[SIMULATED] OTP sent to {phoneNumber}: {otp}");

            return Task.FromResult<string?>(otp);
        }

        public bool ValidateOtp(string phoneNumber, string otpCode)
        {
            if (_cache.TryGetValue($"OTP_{phoneNumber}", out string correctOtp) && correctOtp == otpCode)
            {
                _cache.Remove($"OTP_{phoneNumber}");
                return true;
            }
            return false;
        }
    }
}