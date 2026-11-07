using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartPension.Api.Data;
using SmartPension.Api.Models;
using SmartPension.Api.Services;

namespace SmartPension.Api.Controllers;

public record BankDto(string BankType, string BankName, string Branch, string AccountNumber,
    string AccountHolder, DateTime DateOfBirth, string PensionCode);
public record ProfileDto(string FullName, string Phone, string Nid, DateTime DateOfBirth, DateTime RetirementDate);
public record CalcDto(decimal LastBasic, double Years, decimal CommutePct, decimal TaxPct);
public record FaceDto(string Nonce, string Image, string Image2, string Source);
public record EligibilityInfo(DateTime NextDate, decimal Amount, bool CanRequest, string? Reason);

[ApiController]
[Authorize(Roles = "Pensioner")]
[Route("api/pensioner")]
public class PensionerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;
    public PensionerController(AppDbContext db, AuditService audit, IMemoryCache cache)
    { _db = db; _audit = audit; _cache = cache; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private Task<Pensioner> Me() => _db.Pensioners.FirstAsync(p => p.UserId == UserId);

    public static EligibilityInfo Eligibility(Pensioner p, IEnumerable<WithdrawalRequest> ws)
    {
        var last = ws.Where(w => w.Status is WithdrawalStatus.Approved or WithdrawalStatus.Completed)
                     .OrderByDescending(w => w.RequestedAt).FirstOrDefault()?.RequestedAt ?? p.RegisteredAt;
        var next = last.AddDays(30);
        var verified = p.LastLifeVerification.HasValue && p.LastLifeVerification > DateTime.UtcNow.AddDays(-365);
        var hasPending = ws.Any(w => w.Status == WithdrawalStatus.Pending);
        string? reason = null;
        if (!p.IsApproved) reason = "Your account is waiting for admin approval";
        else if (DateTime.UtcNow < next) reason = "Next withdrawal date not reached";
        else if (!verified) reason = "Life verification required";
        else if (!p.BankVerified) reason = "Bank account not verified yet";
        else if (hasPending) reason = "A request is already pending";
        return new EligibilityInfo(next, p.MonthlyPension, reason == null, reason);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var p = await Me();
        var ws = await _db.Withdrawals.Where(w => w.PensionerId == p.Id).ToListAsync();
        var acc = p.AccountNumber;
        return Ok(new
        {
            p.PensionId, p.PensionCode, p.IsApproved, p.FullName, p.Nid, p.Phone, p.Gender, p.Address, p.NomineeName, p.NomineeRelation, p.DateOfBirth, p.RetirementDate,
            p.PensionType, AgeAtRetirement = Catalog.AgeAt(p.DateOfBirth, p.RetirementDate),
            MinAge = Catalog.MinRetirementAge.GetValueOrDefault(p.PensionType, 0), p.MonthlyPension, p.BankType, p.BankName, p.Branch,
            AccountMasked = acc.Length > 4 ? new string('*', acc.Length - 4) + acc[^4..] : acc,
            p.AccountHolder, p.BankVerified, p.LastLifeVerification,
            FaceEnrolled = p.FaceReference != null,
            Eligibility = Eligibility(p, ws)
        });
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal()
    {
        var p = await Me();
        var ws = await _db.Withdrawals.Where(w => w.PensionerId == p.Id).ToListAsync();
        var e = Eligibility(p, ws);
        if (!e.CanRequest) return BadRequest(e.Reason);
        _db.Withdrawals.Add(new WithdrawalRequest { PensionerId = p.Id, Amount = p.MonthlyPension });
        _db.Notifications.Add(new Notification { UserId = UserId, Message = "Withdrawal request submitted (Pending)." });
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, "Withdrawal requested");
        return Ok();
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> History()
    {
        var p = await Me();
        var all = await _db.Withdrawals.Where(w => w.PensionerId == p.Id)
            .OrderByDescending(w => w.RequestedAt).ToListAsync();
        return Ok(new
        {
            items = all.Select(w => new { w.Id, w.RequestedAt, w.Amount, Status = w.Status.ToString(), w.AdminNote }),
            totalWithdrawn = all.Where(w => w.Status == WithdrawalStatus.Completed).Sum(w => w.Amount),
            totalRequested = all.Where(w => w.Status != WithdrawalStatus.Rejected).Sum(w => w.Amount),
            count = all.Count
        });
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(CalcDto d)
    {
        var p = await Me();
        if (d.LastBasic <= 0 || d.Years <= 0) return BadRequest("Enter salary and years of service");
        return Ok(PensionCalculator.Calculate(p.PensionType, d.LastBasic, d.Years,
            Math.Clamp(d.CommutePct, 0, 100), Math.Clamp(d.TaxPct, 0, 100), p.RetirementDate));
    }

    // Life verification is CAMERA ONLY. The manual "I am alive" button was removed.
    // Step 1: client asks for a one-time challenge (random action + nonce, valid 2 minutes).
    private static readonly string[] Challenges =
        { "Blink twice", "Turn your head slowly to the left", "Smile at the camera" };

    [HttpGet("face/challenge")]
    public IActionResult FaceChallenge()
    {
        var nonce = Guid.NewGuid().ToString("N");
        var instruction = Challenges[Random.Shared.Next(Challenges.Length)];
        _cache.Set($"fc:{UserId}:{nonce}", instruction, TimeSpan.FromMinutes(2));
        return Ok(new { nonce, instruction });
    }

    // Step 2: client sends two LIVE frames captured from getUserMedia (before and after the challenge).
    // PROTOTYPE: the server rejects replayed/expired nonces, identical frames, non-JPEG data and any
    // source other than "camera", but a browser cannot be forced to prove the pixels came from a camera.
    // For production use a real liveness-detection + face-matching provider.
    [HttpPost("face-verify")]
    public async Task<IActionResult> FaceVerify(FaceDto d)
    {
        var key = $"fc:{UserId}:{d.Nonce}";
        if (string.IsNullOrEmpty(d.Nonce) || !_cache.TryGetValue(key, out _))
            return BadRequest("Challenge expired. Start again with the camera.");
        _cache.Remove(key); // single use

        const string prefix = "data:image/jpeg;base64,";
        if (d.Source != "camera" || d.Image == null || d.Image2 == null ||
            !d.Image.StartsWith(prefix) || !d.Image2.StartsWith(prefix) ||
            d.Image.Length < 1000 || d.Image2.Length < 1000)
            return BadRequest("Live camera capture required. Gallery or file upload is not allowed.");
        if (d.Image == d.Image2)
            return BadRequest("Liveness check failed. Please perform the requested action in front of the camera.");

        var p = await Me();
        if (p.FaceReference == null)
        {
            p.FaceReference = d.Image;
            await _db.SaveChangesAsync();
            await _audit.Log(User.Identity!.Name!, "Face enrolled via live camera (demo)");
            return Ok(new { enrolled = true, message = "Face enrolled. Now verify again with the camera." });
        }
        var score = Math.Round(0.85 + Random.Shared.NextDouble() * 0.14, 2); // simulated match
        p.LastLifeVerification = DateTime.UtcNow;
        _db.LifeVerifications.Add(new LifeVerification { PensionerId = p.Id, Method = "Face-Camera", Score = score });
        _db.Notifications.Add(new Notification { UserId = UserId, Message = "Life verification (live camera) successful." });
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, "Life verification via live camera (demo)");
        return Ok(new { enrolled = false, verified = true, score });
    }

    // Pensioner edits own customer information. Pension code is NOT editable.
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(ProfileDto d)
    {
        if (string.IsNullOrWhiteSpace(d.FullName) || string.IsNullOrWhiteSpace(d.Phone))
            return BadRequest("Name and phone are required");
        if (!System.Text.RegularExpressions.Regex.IsMatch(d.Nid ?? "", @"^(\d{10}|\d{13}|\d{17})$"))
            return BadRequest("NID must be 10, 13 or 17 digits");
        if (d.RetirementDate <= d.DateOfBirth) return BadRequest("Retirement date must be after date of birth");
        var p = await Me();
        if (!Catalog.PhoneOk(d.Phone)) return BadRequest("Phone must be a Bangladesh mobile number like 01712345678");
        if (d.DateOfBirth.Date > DateTime.UtcNow.Date) return BadRequest("Date of birth cannot be in the future");
        var nidErr = Catalog.NidDobError(d.Nid!, d.DateOfBirth);
        if (nidErr != null) return BadRequest(nidErr);
        if (await _db.Pensioners.AnyAsync(x => x.Phone == d.Phone && x.Id != p.Id))
            return BadRequest("This phone number is already registered");
        var ageErr = Catalog.CheckDatesAndAge(p.PensionType, d.DateOfBirth, d.RetirementDate);
        if (ageErr != null) return BadRequest(ageErr);
        if (await _db.Pensioners.AnyAsync(x => x.Nid == d.Nid && x.Id != p.Id))
            return BadRequest("This NID is already registered");
        p.FullName = d.FullName.Trim(); p.Phone = d.Phone.Trim(); p.Nid = d.Nid!;
        p.DateOfBirth = d.DateOfBirth; p.RetirementDate = d.RetirementDate;
        var u = await _db.Users.FindAsync(UserId);
        if (u is ApplicationUser au) au.FullName = p.FullName;
        _db.Notifications.Add(new Notification { UserId = UserId, Message = "Your information was updated." });
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, "Profile updated");
        return Ok();
    }

    [HttpPut("bank")]
    public async Task<IActionResult> UpdateBank(BankDto d)
    {
        var p = await Me();
        // identity check: date of birth and 11-digit pension code must match the registered record
        if (d.PensionCode != p.PensionCode || d.DateOfBirth.Date != p.DateOfBirth.Date)
            return BadRequest("Date of birth or pension code does not match our records");
        if (!Catalog.Banks.TryGetValue(d.BankType ?? "", out var banks) || !banks.Contains(d.BankName))
            return BadRequest("Invalid bank type or bank name");
        var accErr = Catalog.AccountError(d.BankType!, d.AccountNumber);
        if (accErr != null) return BadRequest(accErr);
        if (Catalog.Norm(d.AccountHolder) != Catalog.Norm(p.FullName))
            return BadRequest("Account holder name must match your full name");
        p.BankType = d.BankType!; p.BankName = d.BankName; p.Branch = d.Branch;
        p.AccountNumber = d.AccountNumber; p.AccountHolder = d.AccountHolder;
        p.BankVerified = false; // admin must re-verify
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, "Bank details updated");
        return Ok();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications() =>
        Ok(await _db.Notifications.Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAt).Take(30).ToListAsync());
}
