using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Services;

public interface IProductService
{
    Task<List<ProductResponse>> GetAllAsync();
    Task<ProductResponse?> GetByIdAsync(Guid id);
    Task<ProductResponse> CreateAsync(ProductRequest request);
    Task<ProductResponse?> UpdateAsync(Guid id, ProductRequest request);
}

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db) => _db = db;

    public async Task<List<ProductResponse>> GetAllAsync()
    {
        return await _db.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id)
    {
        var p = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : MapToResponse(p);
    }

    public async Task<ProductResponse> CreateAsync(ProductRequest request)
    {
        var product = new Product
        {
            Code = request.Code,
            Name = request.Name,
            CategoryId = request.CategoryId,
            Remarks = request.Remarks
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await _db.Entry(product).Reference(p => p.Category).LoadAsync();
        return MapToResponse(product);
    }

    public async Task<ProductResponse?> UpdateAsync(Guid id, ProductRequest request)
    {
        var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return null;

        product.Code = request.Code;
        product.Name = request.Name;
        product.CategoryId = request.CategoryId;
        product.Remarks = request.Remarks;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _db.Entry(product).Reference(p => p.Category).LoadAsync();
        return MapToResponse(product);
    }

    private static ProductResponse MapToResponse(Product p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name ?? string.Empty,
        Remarks = p.Remarks,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
