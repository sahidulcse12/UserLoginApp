namespace UserLoginApp.Api.Models.Entities;

public class Feature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RoleFeature> RoleFeatures { get; set; } = new List<RoleFeature>();
}
