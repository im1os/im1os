using iM1os.Domain.Common;
using iM1os.Domain.Identity;

namespace iM1os.Domain.Employees;

public sealed class Employee : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string? EmployeeNumber { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public required string DisplayName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public string EmploymentType { get; set; } = "Employee";

    public string Status { get; set; } = "Active";

    public DateOnly? HireDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool IsTechnician { get; set; }

    public bool IsServiceAdvisor { get; set; }

    public bool IsSales { get; set; }

    public bool IsParts { get; set; }

    public bool IsAccounting { get; set; }

    public bool IsInventory { get; set; }

    public bool IsManager { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ApplicationUser? LoginAccount { get; set; }

    public ICollection<EmployeeCompensation> CompensationRecords { get; } = new List<EmployeeCompensation>();

    public ICollection<EmployeeDocument> Documents { get; } = new List<EmployeeDocument>();
}
