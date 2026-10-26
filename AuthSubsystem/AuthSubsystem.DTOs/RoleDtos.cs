namespace AuthSubsystem.DTOs;

public class KeycloakUserRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string UserType { get; set; } = "mis"; // "mis", "corporate", "mybdjobs"
    public List<string> Groups { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public List<int> ModuleIds { get; set; } = new();
    public string? ViewAccess { get; set; }
    public bool IsAdmin { get; set; }
    public List<int> SubLinkIds { get; set; } = new();
    public int? DepartmentId { get; set; }
    public bool AccessFromHome { get; set; }
    public Dictionary<string, string> CustomAttributes { get; set; } = new();
}

public class UserPermissionsDto
{
    public string Username { get; set; } = string.Empty;
    public string UserType { get; set; } = "mis";
    public List<string> Groups { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public List<int> ModuleIds { get; set; } = new();
    public string? ViewAccess { get; set; }
    public bool IsAdmin { get; set; }
    public List<int> SubLinkIds { get; set; } = new();
    public int? DepartmentId { get; set; }
    public bool AccessFromHome { get; set; }
    public Dictionary<string, string> CustomAttributes { get; set; } = new();
}

public class AssignRoleRequest
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "staff"; // e.g. "admin", "manager", "staff", "recruiter", "jobseeker"
    public string? UserType { get; set; } // "mis", "corporate", "mybdjobs" (optional, will auto-sync to group)
    public List<int>? ModuleIds { get; set; }
    public string? ViewAccess { get; set; } // e.g. "view,edit,delete" or "view"
    public List<int>? SubLinkIds { get; set; }
    public int? DepartmentId { get; set; }
    public bool? AccessFromHome { get; set; }
    public Dictionary<string, string>? AdditionalAttributes { get; set; }
}
