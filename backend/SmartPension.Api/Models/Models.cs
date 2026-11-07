using Microsoft.AspNetCore.Identity;

namespace SmartPension.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
}

public enum WithdrawalStatus { Pending, Approved, Rejected, Completed }

public class Pensioner
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string PensionId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Nid { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public DateTime RetirementDate { get; set; }
    public string PensionCode { get; set; } = "";   // unique 11-digit pension code
    public string PensionType { get; set; } = "";
    public string BankType { get; set; } = "";
    public string Branch { get; set; } = "";
    public decimal MonthlyPension { get; set; } = 15000;
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolder { get; set; } = "";
    public bool BankVerified { get; set; }
    public string? FaceReference { get; set; }          // demo: stored base64 image
    public DateTime? LastLifeVerification { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = true;     // new accounts are active at once; admin may untick to block withdrawals
    public string Gender { get; set; } = "";
    public string Address { get; set; } = "";
    public string NomineeName { get; set; } = "";
    public string NomineeRelation { get; set; } = "";
    public DateTime? ConsentAt { get; set; }        // terms & privacy accepted at sign up
}

public class WithdrawalRequest
{
    public int Id { get; set; }
    public int PensionerId { get; set; }
    public Pensioner? Pensioner { get; set; }
    public decimal Amount { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;
    public DateTime? ProcessedAt { get; set; }
    public string? AdminNote { get; set; }
}

public class LifeVerification
{
    public int Id { get; set; }
    public int PensionerId { get; set; }
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "Manual";   // Manual | Face
    public double? Score { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
