using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPension.Api.Data;

namespace SmartPension.Api.Controllers;

public record AskDto(string Question);

[ApiController]
[Authorize(Roles = "Pensioner")]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AppDbContext _db;
    public AssistantController(AppDbContext db) => _db = db;

    // Simple rule-based assistant using the pensioner's own data.
    // To use a real LLM, replace this logic with a call to the Claude API (keep the API key on the server).
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(AskDto d)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var p = await _db.Pensioners.FirstAsync(x => x.UserId == uid);
        var ws = await _db.Withdrawals.Where(w => w.PensionerId == p.Id).ToListAsync();
        var e = PensionerController.Eligibility(p, ws);
        var q = (d.Question ?? "").ToLowerInvariant();

        string a;
        if (q.Contains("withdraw") || q.Contains("next") || q.Contains("when"))
            a = $"Your next eligible withdrawal date is {e.NextDate:dd MMM yyyy} for {p.MonthlyPension:N0} BDT. " +
                (e.CanRequest ? "You can request it now from the dashboard." : $"Right now: {e.Reason}.");
        else if (q.Contains("verif") || q.Contains("life") || q.Contains("face"))
            a = "Life verification is needed once a year. Go to 'Verification' and complete the live camera check (photos from the gallery are not accepted). " +
                (p.LastLifeVerification.HasValue ? $"Your last verification was on {p.LastLifeVerification:dd MMM yyyy}." : "You have not verified yet.");
        else if (q.Contains("bank") || q.Contains("account"))
            a = $"Your registered bank is {p.BankName}. Status: {(p.BankVerified ? "Verified" : "Waiting for admin verification")}. You can update details in the 'Bank' section.";
        else if (q.Contains("pension id") || q.Contains("id"))
            a = $"Your Pension ID is {p.PensionId}. Use it to log in.";
        else if (q.Contains("amount") || q.Contains("how much"))
            a = $"Your monthly pension is {p.MonthlyPension:N0} BDT.";
        else if (q.Contains("status") || q.Contains("request"))
            a = ws.Any() ? $"Your latest request is {ws.OrderByDescending(w => w.RequestedAt).First().Status}." : "You have no withdrawal requests yet.";
        else if (q.Contains("help") || q.Contains("contact") || q.Contains("support"))
            a = "Customer care: +880-1700-000000, support@smartpension.gov.bd (Sun-Thu 9 AM - 5 PM).";
        else
            a = "I can help with: next withdrawal, verification, bank details, pension amount, request status, and support contact. Please ask about one of these.";
        return Ok(new { answer = a });
    }
}
