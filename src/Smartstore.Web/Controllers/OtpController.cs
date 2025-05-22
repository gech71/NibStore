using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Smartstore.Services.Otp;

[Route("otp")]
public class OtpController : Controller
{
    private readonly IOtpService _otpService;
    private readonly ILogger<OtpController> _logger;

    public OtpController(IOtpService otpService, ILogger<OtpController> logger)
    {
        _otpService = otpService;
        _logger = logger;
    }

   [HttpPost("send")]
public async Task<IActionResult> SendOtp([FromForm] string phoneNumber)
{
    if (string.IsNullOrWhiteSpace(phoneNumber))
        return BadRequest(new { message = "Phone number is required." });

    var otp = await _otpService.SendOtpAsync(phoneNumber);

    if (otp != null)
        return Ok(new { message = "OTP sent successfully.", otp });
    else
        return StatusCode(500, new { message = "Failed to send OTP." });
}


    [HttpPost("verify")]
    public IActionResult VerifyOtp([FromForm] string phoneNumber, [FromForm] string otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode))
            return BadRequest(new { message = "OTP code is required." });

        var isValid = _otpService.ValidateOtp(phoneNumber, otpCode);

        if (!isValid)
            return BadRequest(new { message = "Invalid or expired OTP." });

        // OTP is valid: proceed to log in or redirect
        return Ok(new { message = "OTP verified successfully." });
    }

}