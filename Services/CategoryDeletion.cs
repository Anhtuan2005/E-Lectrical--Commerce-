using EcommerceApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EcommerceApp.Services;

public enum CategoryDeleteResult { Deleted, NotFound, InUse }

public static class CategoryDeletion
{
    public static Task<CategoryDeleteResult> DeleteAsync(AppDbContext db, int id) =>
        DatabaseTransaction.ExecuteAsync(db, async () =>
        {
            var category = await db.Categories
                .FromSqlInterpolated($"SELECT * FROM Categories WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
                .SingleOrDefaultAsync();
            if (category is null) return CategoryDeleteResult.NotFound;
            // Archived products still own their category and are referenced by order history.
            if (await db.Products.IgnoreQueryFilters().AnyAsync(product => product.CategoryId == id))
                return CategoryDeleteResult.InUse;
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return CategoryDeleteResult.Deleted;
        }, IsolationLevel.Serializable);
}
