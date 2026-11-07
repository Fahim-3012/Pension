using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartPension.Api.Data;
using SmartPension.Api.Models;

namespace SmartPension.Api.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;
    public async Task Log(string user, string action)
    {
        _db.AuditLogs.Add(new AuditLog { UserName = user, Action = action });
        await _db.SaveChangesAsync();
    }
}

public class TokenService
{
    private readonly IConfiguration _c;
    public TokenService(IConfiguration c) => _c = c;

    public string Create(ApplicationUser u, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id),
            new Claim(ClaimTypes.Name, u.UserName ?? ""),
            new Claim(ClaimTypes.Role, role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_c["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            _c["Jwt:Issuer"], _c["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class Catalog
{
    // pension type -> rate of last basic salary paid at 25+ years of service
    public static readonly Dictionary<string, double> PensionTypes = new()
    {
        ["Superannuation (Regular Retirement)"] = 0.50,
        ["Compulsory Retirement"] = 0.40,
        ["Invalid (Disability) Pension"] = 0.45,
        ["Family Pension"] = 0.30,
        ["Compensation Pension"] = 0.35
    };
    public static readonly Dictionary<string, string[]> Banks = new()
    {
        ["State-owned Commercial"] = new[] { "Sonali Bank", "Janata Bank", "Agrani Bank", "Rupali Bank" },
        ["Private Commercial"] = new[] { "Dutch-Bangla Bank", "BRAC Bank", "City Bank", "Eastern Bank" },
        ["Islamic"] = new[] { "Islami Bank Bangladesh", "Al-Arafah Islami Bank", "Social Islami Bank" },
        ["Specialized"] = new[] { "Bangladesh Krishi Bank", "Rajshahi Krishi Unnayan Bank" },
        ["Mobile Financial Service"] = new[] { "bKash", "Nagad", "Rocket" }
    };

    // Year limit: dates of birth / retirement cannot be later than this year.
    public const int MaxYear = 2026;
    // Minimum age (in full years) that must be reached on the retirement date. DEMO VALUES - change to your rules.
    public static readonly Dictionary<string, int> MinRetirementAge = new()
    {
        ["Superannuation (Regular Retirement)"] = 59,
        ["Compulsory Retirement"] = 50,
        ["Invalid (Disability) Pension"] = 18,
        ["Family Pension"] = 0,
        ["Compensation Pension"] = 18
    };
    public static int AgeAt(DateTime dob, DateTime on)
    {
        var a = on.Year - dob.Year;
        if (on.Date < dob.Date.AddYears(a)) a--;
        return a;
    }
    public static string? CheckDatesAndAge(string type, DateTime dob, DateTime retirement)
    {
        if (dob.Year > MaxYear || retirement.Year > MaxYear) return $"Year limit is {MaxYear}";
        var min = MinRetirementAge.GetValueOrDefault(type, 0);
        var age = AgeAt(dob, retirement);
        return age < min ? $"Minimum age for {type} is {min} years. Age at retirement: {age}" : null;
    }

    // ---- sign-up validation helpers ----
    public static bool PhoneOk(string? p) =>
        System.Text.RegularExpressions.Regex.IsMatch(p ?? "", @"^01[3-9]\d{8}$");   // Bangladesh mobile
    public static string Norm(string? s) =>
        string.Join(' ', (s ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    // 17-digit NID numbers start with the birth year.
    public static string? NidDobError(string nid, DateTime dob) =>
        nid.Length == 17 && nid[..4] != dob.Year.ToString() ? "A 17-digit NID must start with your birth year" : null;
    public static string? AccountError(string bankType, string? acc)
    {
        acc = (acc ?? "").Trim();
        if (bankType == "Mobile Financial Service")
            return PhoneOk(acc) ? null : "Mobile banking number must be a Bangladesh mobile number like 01712345678";
        return System.Text.RegularExpressions.Regex.IsMatch(acc, @"^\d{8,20}$") ? null : "Account number must be 8 to 20 digits";
    }

    public static string NewPensionCode(AppDbContext db)
    {
        string code;
        do { code = Random.Shared.NextInt64(10_000_000_000, 100_000_000_000).ToString(); }
        while (db.Pensioners.Any(p => p.PensionCode == code));
        return code; // always 11 digits
    }
}

// DEMO FORMULA - replace with your government/organisation rules.
// gross = lastBasic x typeRate x min(years,25)/25 ; commuted part becomes a lump sum (x12 months x10 years);
// net monthly = (gross - commuted) - tax%.
public static class PensionCalculator
{
    public static object Calculate(string type, decimal basic, double years, decimal commutePct, decimal taxPct, DateTime retirement)
    {
        var rate = (decimal)Catalog.PensionTypes.GetValueOrDefault(type, 0.5);
        var gross = Math.Round(basic * rate * (decimal)Math.Min(years, 25) / 25m, 2);
        var commuted = gross * commutePct / 100m;
        var lump = Math.Round(commuted * 12m * 10m, 2);
        var remaining = gross - commuted;
        var tax = Math.Round(remaining * taxPct / 100m, 2);
        return new { retirementDate = retirement, pensionType = type, gross, lumpSum = lump, tax, netMonthly = Math.Round(remaining - tax, 2) };
    }
}
