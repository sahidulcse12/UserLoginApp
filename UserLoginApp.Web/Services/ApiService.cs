using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UserLoginApp.Web.Models;

namespace UserLoginApp.Web.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly TokenProvider _tokenProvider;

    public ApiService(string baseUrl, TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = !string.IsNullOrEmpty(_tokenProvider.AccessToken)
            ? new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken)
            : null;
    }

    // Me
    public async Task<MeResponse?> GetMeAsync()
    {
        SetAuthHeader();
        try { return await _http.GetFromJsonAsync<MeResponse>("api/me"); }
        catch { return null; }
    }

    // Users
    public async Task<List<UserDto>> GetUsersAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new();
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<UserDto>($"api/users/{id}");
    }

    public async Task<(bool Success, string Error)> CreateUserAsync(CreateUserModel model)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("api/users", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string Error)> UpdateUserAsync(Guid id, UpdateUserModel model)
    {
        SetAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/users/{id}", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        SetAuthHeader();
        return (await _http.DeleteAsync($"api/users/{id}")).IsSuccessStatusCode;
    }

    public async Task<bool> SetUserActiveAsync(Guid id, bool active)
    {
        SetAuthHeader();
        var endpoint = active ? $"api/users/{id}/activate" : $"api/users/{id}/deactivate";
        return (await _http.PutAsync(endpoint, null)).IsSuccessStatusCode;
    }

    public async Task<bool> AssignUserRoleAsync(Guid userId, Guid roleId)
    {
        SetAuthHeader();
        return (await _http.PostAsJsonAsync($"api/users/{userId}/roles", new { roleId })).IsSuccessStatusCode;
    }

    public async Task<bool> RemoveUserRoleAsync(Guid userId, Guid roleId)
    {
        SetAuthHeader();
        return (await _http.DeleteAsync($"api/users/{userId}/roles/{roleId}")).IsSuccessStatusCode;
    }

    // Roles
    public async Task<List<RoleDto>> GetRolesAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<RoleDto>>("api/roles") ?? new();
    }

    public async Task<RoleDto?> GetRoleAsync(Guid id)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<RoleDto>($"api/roles/{id}");
    }

    public async Task<List<FeatureDto>> GetAllFeaturesAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<FeatureDto>>("api/roles/features") ?? new();
    }

    public async Task<(bool Success, string Error)> CreateRoleAsync(RoleFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("api/roles", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string Error)> UpdateRoleAsync(Guid id, RoleFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/roles/{id}", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteRoleAsync(Guid id)
    {
        SetAuthHeader();
        return (await _http.DeleteAsync($"api/roles/{id}")).IsSuccessStatusCode;
    }

    public async Task<bool> AssignRoleFeatureAsync(Guid roleId, Guid featureId)
    {
        SetAuthHeader();
        return (await _http.PostAsJsonAsync($"api/roles/{roleId}/features", new { featureId })).IsSuccessStatusCode;
    }

    public async Task<bool> RemoveRoleFeatureAsync(Guid roleId, Guid featureId)
    {
        SetAuthHeader();
        return (await _http.DeleteAsync($"api/roles/{roleId}/features/{featureId}")).IsSuccessStatusCode;
    }

    // Categories
    public async Task<List<CategoryDto>> GetCategoriesAsync(string? search = null)
    {
        SetAuthHeader();
        var url = string.IsNullOrEmpty(search) ? "api/categories" : $"api/categories?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<CategoryDto>>(url) ?? new();
    }

    public async Task<List<CategoryDto>> GetCategoriesLookupAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<CategoryDto>>("api/categories/lookup") ?? new();
    }

    public async Task<CategoryDto?> GetCategoryAsync(Guid id)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<CategoryDto>($"api/categories/{id}");
    }

    public async Task<(bool Success, string Error)> CreateCategoryAsync(CategoryFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("api/categories", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string Error)> UpdateCategoryAsync(Guid id, CategoryFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/categories/{id}", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> SetCategoryActiveAsync(Guid id, bool active)
    {
        SetAuthHeader();
        var endpoint = active ? $"api/categories/{id}/activate" : $"api/categories/{id}/deactivate";
        return (await _http.PutAsync(endpoint, null)).IsSuccessStatusCode;
    }

    // Products
    public async Task<List<ProductDto>> GetProductsAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<ProductDto>>("api/products") ?? new();
    }

    public async Task<ProductDto?> GetProductAsync(Guid id)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<ProductDto>($"api/products/{id}");
    }

    public async Task<(bool Success, string Error)> CreateProductAsync(ProductFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("api/products", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string Error)> UpdateProductAsync(Guid id, ProductFormModel model)
    {
        SetAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/products/{id}", model);
        if (response.IsSuccessStatusCode) return (true, string.Empty);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
