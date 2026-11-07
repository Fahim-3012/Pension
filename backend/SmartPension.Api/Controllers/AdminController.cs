using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPension.Api.Data;
using SmartPension.Api.Models;
using SmartPension.Api.Services;

namespace SmartPension.Api.Controllers;

public record PensionerUpdateDto(decimal MonthlyPension, bool IsActive, bool BankVerified, bool IsApproved);
public record DecisionDto(string Status, string? Note);

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public AdminController(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet("pensioners")]
    public async Task<IActionResult> Pensioners() =>
        Ok(await _db.Pensioners.Select(p => new
        {
            p.Id, p.IsApproved, p.Gender, p.Address, p.NomineeName, p.NomineeRelation, p.PensionId, p.PensionCode, p.Nid, p.DateOfBirth, p.RetirementDate, p.PensionType, p.BankType, p.FullName, p.Phone, p.MonthlyPension, p.BankName,
            p.AccountNumber, p.BankVerified, p.IsActive, p.LastLifeVerification
        }).ToListAsync());

    [HttpPut("pensioners/{id}")]
    public async Task<IActionResult> UpdatePensioner(int id, PensionerUpdateDto d)
    {
        var p = await _db.Pensioners.FindAsync(id);
        if (p == null) return NotFound();
        var newlyApproved = !p.IsApproved && d.IsApproved;
        p.MonthlyPension = d.MonthlyPension; p.IsActive = d.IsActive; p.BankVerified = d.BankVerified; p.IsApproved = d.IsApproved;
        if (newlyApproved)
            _db.Notifications.Add(new Notification { UserId = p.UserId, Message = "Your account has been approved. You can now log in." });
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, $"Updated pensioner {p.PensionId}");
        return Ok();
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> Withdrawals([FromQuery] string? status)
    {
        var q = _db.Withdrawals.Include(w => w.Pensioner).AsQueryable();
        if (Enum.TryParse<WithdrawalStatus>(status, out var s)) q = q.Where(w => w.Status == s);
        return Ok(await q.OrderByDescending(w => w.RequestedAt).Select(w => new
        {
            w.Id, PensionId = w.Pensioner!.PensionId, Name = w.Pensioner.FullName,
            w.Amount, w.RequestedAt, Status = w.Status.ToString(), w.AdminNote
        }).ToListAsync());
    }

    [HttpPost("withdrawals/{id}/decision")]
    public async Task<IActionResult> Decide(int id, DecisionDto d)
    {
        var w = await _db.Withdrawals.Include(x => x.Pensioner).FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return NotFound();
        if (!Enum.TryParse<WithdrawalStatus>(d.Status, out var st) || st == WithdrawalStatus.Pending)
            return BadRequest("Invalid status");
        w.Status = st; w.AdminNote = d.Note; w.ProcessedAt = DateTime.UtcNow;
        _db.Notifications.Add(new Notification
        {
            UserId = w.Pensioner!.UserId,
            Message = $"Your withdrawal request #{w.Id} is now {st}." + (string.IsNullOrEmpty(d.Note) ? "" : $" Note: {d.Note}")
        });
        await _db.SaveChangesAsync();
        await _audit.Log(User.Identity!.Name!, $"Withdrawal #{id} -> {st}");
        return Ok();
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports()
    {
        var since = DateTime.UtcNow.AddDays(-365);
        return Ok(new
        {
            totalPensioners = await _db.Pensioners.CountAsync(),
            activePensioners = await _db.Pensioners.CountAsync(p => p.IsActive),
            pendingApprovals = await _db.Pensioners.CountAsync(p => !p.IsApproved),
            pending = await _db.Withdrawals.CountAsync(w => w.Status == WithdrawalStatus.Pending),
            approved = await _db.Withdrawals.CountAsync(w => w.Status == WithdrawalStatus.Approved),
            rejected = await _db.Withdrawals.CountAsync(w => w.Status == WithdrawalStatus.Rejected),
            completed = await _db.Withdrawals.CountAsync(w => w.Status == WithdrawalStatus.Completed),
            totalPaid = await _db.Withdrawals.Where(w => w.Status == WithdrawalStatus.Completed).SumAsync(w => (decimal?)w.Amount) ?? 0,
            verifiedLastYear = await _db.Pensioners.CountAsync(p => p.LastLifeVerification > since),
            overdueVerification = await _db.Pensioners.CountAsync(p => p.LastLifeVerification == null || p.LastLifeVerification <= since)
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit() =>
        Ok(await _db.AuditLogs.OrderByDescending(a => a.At).Take(100).ToListAsync());
}
