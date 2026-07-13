namespace UserLoginApp.Web.Services;

public class UserContext
{
    public List<string> Features { get; private set; } = new();
    public List<string> Roles { get; private set; } = new();
    public string Username { get; private set; } = string.Empty;
    public bool IsLoaded { get; private set; }

    // NavMenu subscribes to this so it can call StateHasChanged() when features arrive
    public event Action? OnChange;

    public void Load(string username, List<string> features, List<string> roles)
    {
        Username = username;
        Features = features;
        Roles = roles;
        IsLoaded = true;
        OnChange?.Invoke();
    }

    public bool HasFeature(string code) => Features.Contains(code);
}
