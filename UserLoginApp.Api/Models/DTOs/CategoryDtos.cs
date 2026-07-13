namespace UserLoginApp.Api.Models.DTOs;

public class CategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
