namespace UserLoginApp.Api.Models.Entities;

public class RoleFeature
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;
}
