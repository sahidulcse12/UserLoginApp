using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync(string? search = null);
    Task<CategoryResponse?> GetByIdAsync(Guid id);
    Task<CategoryResponse> CreateAsync(CategoryRequest request);
    Task<CategoryResponse?> UpdateAsync(Guid id, CategoryRequest request);
    Task<bool> SetActiveAsync(Guid id, bool isActive);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db) => _db = db;

    public async Task<List<CategoryResponse>> GetAllAsync(string? search = null)
    {
        var query = _db.Categories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));

        return await query
            .OrderBy(c => c.Name)
            .Select(c => MapToResponse(c))
            .ToListAsync();
    }

    public async Task<CategoryResponse?> GetByIdAsync(Guid id)
    {
        var cat = await _db.Categories.FindAsync(id);
        return cat == null ? null : MapToResponse(cat);
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request)
    {
        var cat = new Category { Name = request.Name, Code = request.Code };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return MapToResponse(cat);
    }

    public async Task<CategoryResponse?> UpdateAsync(Guid id, CategoryRequest request)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return null;

        cat.Name = request.Name;
        cat.Code = request.Code;
        cat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToResponse(cat);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return false;
        cat.IsActive = isActive;
        cat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static CategoryResponse MapToResponse(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Code = c.Code,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
