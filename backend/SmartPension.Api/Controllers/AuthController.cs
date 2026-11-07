using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPension.Api.Data;
using SmartPension.Api.Models;
using SmartPension.Api.Services;

namespace SmartPension.Api.Controllers;

public record RegisterDto(string FullName, string Email, string Password, string Phone, string PensionId,
    string Nid, DateTime DateOfBirth, DateTime RetirementDate, string PensionType,
    string BankType, string BankName, string Branch, string AccountNumber, string AccountHolder,
    string Gender, string Address, string NomineeName, string NomineeRelation, bool AcceptedTerms);
public record LoginDto(string Identifier, string Password);
public record AdminLoginDto(string Email, string Password, string? MfaCode);

[ApiController]
[EnableRateLimiting("auth")]   // limits sign-up / login attempts per IP (anti-spam / brute force)
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _um;
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;
    private readonly ILogger<AuthController> _log;

    public AuthController(UserManager<ApplicationUser> um, AppDbContext db,
        TokenService tokens, AuditService audit, ILogger<AuthController> log)
    { _um = um; _db = db; _tokens = tokens; _audit = audit; _log = log; }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto d)
    {
        var pid = (d.PensionId ?? "").Trim().ToUpperInvariant();
        if (!d.AcceptedTerms) return BadRequest("You must accept the Terms and Privacy Policy");
        if (string.IsNullOrWhiteSpace(d.FullName)) return BadRequest("Full name is required");
        if (!Regex.IsMatch(pid, @"^[A-Z0-9-]{6,20}$")) return BadRequest("Pension ID must be 6-20 letters, numbers or hyphens");
        if (await _db.Pensioners.AnyAsync(x => x.PensionId == pid)) return BadRequest("This Pension ID is already registered");
        if (!Regex.IsMatch(d.Nid ?? "", @"^(\d{10}|\d{13}|\d{17})$")) return BadRequest("NID must be 10, 13 or 17 digits");
        if (await _db.Pensioners.AnyAsync(x => x.Nid == d.Nid)) return BadRequest("This NID is already registered");
        if (d.DateOfBirth.Date > DateTime.UtcNow.Date) return BadRequest("Date of birth cannot be in the future");
        var nidErr = Catalog.NidDobError(d.Nid!, d.DateOfBirth);
        if (nidErr != null) return BadRequest(nidErr);
        if (!Catalog.PhoneOk(d.Phone)) return BadRequest("Phone must be a Bangladesh mobile number like 01712345678");
        if (await _db.Pensioners.AnyAsync(x => x.Phone == d.Phone)) return BadRequest("This phone number is already registered");
        if (!new[] { "Male", "Female", "Other" }.Contains(d.Gender)) return BadRequest("Select gender");
        if (string.IsNullOrWhiteSpace(d.Address)) return BadRequest("Address is required");
        if (string.IsNullOrWhiteSpace(d.NomineeName) || string.IsNullOrWhiteSpace(d.NomineeRelation))
            return BadRequest("Nominee name and relation are required");
        if (d.RetirementDate <= d.DateOfBirth) return BadRequest("Retirement date must be after date of birth");
        if (!Catalog.PensionTypes.ContainsKey(d.PensionType ?? "")) return BadRequest("Invalid pension type");
        var ageErr = Catalog.CheckDatesAndAge(d.PensionType!, d.DateOfBirth, d.RetirementDate);
        if (ageErr != null) return BadRequest(ageErr);
        if (!Catalog.Banks.TryGetValue(d.BankType ?? "", out var banks) || !banks.Contains(d.BankName))
            return BadRequest("Invalid bank type or bank name");
        var accErr = Catalog.AccountError(d.BankType!, d.AccountNumber);
        if (accErr != null) return BadRequest(accErr);
        if (Catalog.Norm(d.AccountHolder) != Catalog.Norm(d.FullName))
            return BadRequest("Account holder name must match your full name");

        var user = new ApplicationUser { UserName = d.Email, Email = d.Email, FullName = d.FullName.Trim(), EmailConfirmed = false };
        var res = await _um.CreateAsync(user, d.Password);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));

        try
        {
            await _um.AddToRoleAsync(user, "Pensioner");
            _db.Pensioners.Add(new Pensioner
            {
                UserId = user.Id, PensionId = pid, PensionCode = Catalog.NewPensionCode(_db),
                FullName = d.FullName.Trim(), Phone = d.Phone, Nid = d.Nid!, DateOfBirth = d.DateOfBirth,
                RetirementDate = d.RetirementDate, PensionType = d.PensionType!, Gender = d.Gender,
                Address = d.Address.Trim(), NomineeName = d.NomineeName.Trim(), NomineeRelation = d.NomineeRelation,
                BankType = d.BankType!, BankName = d.BankName, Branch = d.Branch,
                AccountNumber = d.AccountNumber.Trim(), AccountHolder = d.AccountHolder.Trim(),
                IsApproved = true, ConsentAt = DateTime.UtcNow
            });
            _db.Notifications.Add(new Notification { UserId = user.Id, Message = $"Welcome! Your Pension ID is {pid}." });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Registration failed, rolling back user {Email}", d.Email);
            await _um.DeleteAsync(user);   // roll back so the email is not left "taken"
            return StatusCode(500, "Registration failed. Please try again.");
        }
        await _audit.Log(d.Email, "Registered pensioner " + pid + " (auto login)");
        var code = (await _db.Pensioners.FirstAsync(x => x.PensionId == pid)).PensionCode;
        // auto login: the new pensioner gets a token right away
        return Ok(new { pensionId = pid, pensionCode = code, approved = true,
                        token = _tokens.Create(user, "Pensioner"), role = "Pensioner", name = d.FullName.Trim() });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto d)
    {
        // Pensioner can log in with Pension ID, 11-digit Pension Code, or registered email.
        var key = (d.Identifier ?? "").Trim();
        var p = await _db.Pensioners.FirstOrDefaultAsync(x => x.PensionId == key || x.PensionCode == key);
        if (p == null)
        {
            var byMail = await _um.FindByEmailAsync(key);
            if (byMail != null) p = await _db.Pensioners.FirstOrDefaultAsync(x => x.UserId == byMail.Id);
        }
        var user = p == null ? null : await _um.FindByIdAsync(p.UserId);
        if (user == null || !p!.IsActive) return Unauthorized("Invalid credentials");
        if (await _um.IsLockedOutAsync(user)) return Unauthorized("Account locked. Try later.");
        if (!await _um.CheckPasswordAsync(user, d.Password))
        {
            await _um.AccessFailedAsync(user);
            return Unauthorized("Invalid credentials");
        }
        await _um.ResetAccessFailedCountAsync(user);
        await _audit.Log(user.Email!, "Pensioner login");
        return Ok(new { token = _tokens.Create(user, "Pensioner"), role = "Pensioner", name = p.FullName, approved = p.IsApproved });
    }

    [HttpPost("admin/login")]
    public async Task<IActionResult> AdminLogin(AdminLoginDto d)
    {
        var user = await _um.FindByEmailAsync(d.Email);
        if (user == null || !await _um.IsInRoleAsync(user, "Admin")) return Unauthorized("Invalid credentials");
        if (await _um.IsLockedOutAsync(user)) return Unauthorized("Account locked.");
        if (!await _um.CheckPasswordAsync(user, d.Password))
        {
            await _um.AccessFailedAsync(user);
            return Unauthorized("Invalid credentials");
        }

        // MFA step (demo: code is written to the API console instead of sent by email/SMS)
        if (string.IsNullOrWhiteSpace(d.MfaCode))
        {
            var code = await _um.GenerateTwoFactorTokenAsync(user, "Email");
            _log.LogWarning("=== ADMIN MFA CODE: {Code} ===", code);
            return Ok(new { mfaRequired = true });
        }
        if (!await _um.VerifyTwoFactorTokenAsync(user, "Email", d.MfaCode))
            return Unauthorized("Invalid MFA code");

        await _audit.Log(user.Email!, "Admin login (MFA ok)");
        return Ok(new { token = _tokens.Create(user, "Admin"), role = "Admin", name = user.FullName });
    }

    [HttpGet("meta")]
    public IActionResult Meta() => Ok(new { pensionTypes = Catalog.PensionTypes.Keys, banks = Catalog.Banks });

    [HttpGet("support")]
    public IActionResult Support() => Ok(new
    {
        phone = "+880-1700-000000", email = "support@smartpension.gov.bd",
        hours = "Sun-Thu, 9:00 AM - 5:00 PM"
    });
}
