using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Common;
using SupportPilot.Contracts;
using SupportPilot.Domain;

namespace SupportPilot.Application.KnowledgeBase;

/// <summary>
/// Provides public and support-staff knowledge base use cases.
/// </summary>
public sealed class KnowledgeBaseUseCases(
    ISupportPilotDbContext db,
    IApplicationCache cache,
    IOptions<CacheOptions> cacheOptions)
{
    /// <summary>
    /// Lists public knowledge base categories with published article counts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Public category list.</returns>
    public async Task<IReadOnlyList<KnowledgeBaseCategoryResponse>> ListPublicCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheGroups.KnowledgeBase,
            "categories",
            KnowledgeBaseCacheExpiration,
            token => db.KnowledgeBaseCategories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new KnowledgeBaseCategoryResponse(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Articles.Count(article => article.IsPublished)))
                .ToListAsync(token),
            cancellationToken);
    }

    /// <summary>
    /// Searches published knowledge base articles.
    /// </summary>
    /// <param name="search">Optional text search.</param>
    /// <param name="categoryId">Optional category filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Published article list.</returns>
    public async Task<IReadOnlyList<KnowledgeBaseArticleListItemResponse>> ListPublicArticlesAsync(
        string? search,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim();
        var cacheKey = $"articles:search={normalizedSearch?.ToLowerInvariant() ?? "all"}:category={categoryId?.ToString("N") ?? "all"}";
        return await cache.GetOrCreateAsync(
            CacheGroups.KnowledgeBase,
            cacheKey,
            KnowledgeBaseCacheExpiration,
            token =>
            {
                var query = db.KnowledgeBaseArticles
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Where(x => x.IsPublished)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(normalizedSearch))
                {
                    query = query.Where(x => x.Title.Contains(normalizedSearch) || x.Body.Contains(normalizedSearch));
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(x => x.CategoryId == categoryId.Value);
                }

                return query
                    .OrderBy(x => x.Title)
                    .Select(x => new KnowledgeBaseArticleListItemResponse(
                        x.Id,
                        x.Title,
                        x.Slug,
                        x.CategoryId,
                        x.Category.Name,
                        x.IsPublished,
                        x.UpdatedAt))
                    .ToListAsync(token);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets a published article by slug.
    /// </summary>
    /// <param name="slug">Article slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article, or not found.</returns>
    public async Task<ApplicationResult<KnowledgeBaseArticleResponse>> GetPublicArticleAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var article = await cache.GetOrCreateAsync(
            CacheGroups.KnowledgeBase,
            $"article:{normalizedSlug}",
            KnowledgeBaseCacheExpiration,
            async token =>
            {
                var entity = await db.KnowledgeBaseArticles
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .SingleOrDefaultAsync(x => x.Slug == normalizedSlug && x.IsPublished, token);

                return entity is null ? null : ToArticleResponse(entity);
            },
            cancellationToken);

        return article is null
            ? ApplicationResult<KnowledgeBaseArticleResponse>.Failure(ApplicationError.NotFound, "Article not found.")
            : ApplicationResult<KnowledgeBaseArticleResponse>.Success(article);
    }

    /// <summary>
    /// Lists knowledge base categories for support staff.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Category list with total article counts.</returns>
    public async Task<IReadOnlyList<KnowledgeBaseCategoryResponse>> ListAdminCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await db.KnowledgeBaseCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new KnowledgeBaseCategoryResponse(
                x.Id,
                x.Name,
                x.Description,
                x.Articles.Count))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a knowledge base category.
    /// </summary>
    /// <param name="request">Requested category state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created category, or validation error.</returns>
    public async Task<ApplicationResult<KnowledgeBaseCategoryResponse>> CreateCategoryAsync(
        UpsertKnowledgeBaseCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApplicationResult<KnowledgeBaseCategoryResponse>.Failure(
                ApplicationError.Validation,
                "Knowledge base category name is required.");
        }

        var category = new KnowledgeBaseCategory { Name = name, Description = request.Description };
        db.KnowledgeBaseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);

        return ApplicationResult<KnowledgeBaseCategoryResponse>.Success(
            new KnowledgeBaseCategoryResponse(category.Id, category.Name, category.Description, 0));
    }

    /// <summary>
    /// Updates a knowledge base category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="request">Requested category state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated category, or an application error.</returns>
    public async Task<ApplicationResult<KnowledgeBaseCategoryResponse>> UpdateCategoryAsync(
        Guid id,
        UpsertKnowledgeBaseCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await db.KnowledgeBaseCategories
            .Include(x => x.Articles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return ApplicationResult<KnowledgeBaseCategoryResponse>.Failure(
                ApplicationError.NotFound,
                "Knowledge base category not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApplicationResult<KnowledgeBaseCategoryResponse>.Failure(
                ApplicationError.Validation,
                "Knowledge base category name is required.");
        }

        category.Name = name;
        category.Description = request.Description;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);

        return ApplicationResult<KnowledgeBaseCategoryResponse>.Success(new KnowledgeBaseCategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.Articles.Count));
    }

    /// <summary>
    /// Searches all knowledge base articles for support staff.
    /// </summary>
    /// <param name="search">Optional text search.</param>
    /// <param name="categoryId">Optional category filter.</param>
    /// <param name="published">Optional publication filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Article list.</returns>
    public async Task<IReadOnlyList<KnowledgeBaseArticleListItemResponse>> ListAdminArticlesAsync(
        string? search,
        Guid? categoryId,
        bool? published,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim();
        var query = db.KnowledgeBaseArticles
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.Title.Contains(normalizedSearch) ||
                x.Body.Contains(normalizedSearch) ||
                x.Slug.Contains(normalizedSearch));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (published.HasValue)
        {
            query = query.Where(x => x.IsPublished == published.Value);
        }

        return await query
            .OrderBy(x => x.Title)
            .Select(x => new KnowledgeBaseArticleListItemResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.CategoryId,
                x.Category.Name,
                x.IsPublished,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets any article by identifier for support staff.
    /// </summary>
    /// <param name="id">Article identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article, or not found.</returns>
    public async Task<ApplicationResult<KnowledgeBaseArticleResponse>> GetAdminArticleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeBaseArticles
            .AsNoTracking()
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return article is null
            ? ApplicationResult<KnowledgeBaseArticleResponse>.Failure(ApplicationError.NotFound, "Article not found.")
            : ApplicationResult<KnowledgeBaseArticleResponse>.Success(ToArticleResponse(article));
    }

    /// <summary>
    /// Creates a knowledge base article.
    /// </summary>
    /// <param name="request">Requested article state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created article, or validation error.</returns>
    public async Task<ApplicationResult<KnowledgeBaseArticleResponse>> CreateArticleAsync(
        UpsertKnowledgeBaseArticleRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateArticleRequestAsync(request, cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApplicationResult<KnowledgeBaseArticleResponse>.Failure(validation.Error, validation.Message!);
        }

        var article = new KnowledgeBaseArticle();
        ApplyArticleRequest(article, request);

        db.KnowledgeBaseArticles.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);

        var created = await LoadArticleAsync(article.Id, cancellationToken);
        return ApplicationResult<KnowledgeBaseArticleResponse>.Success(ToArticleResponse(created!));
    }

    /// <summary>
    /// Updates a knowledge base article.
    /// </summary>
    /// <param name="id">Article identifier.</param>
    /// <param name="request">Requested article state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated article, or an application error.</returns>
    public async Task<ApplicationResult<KnowledgeBaseArticleResponse>> UpdateArticleAsync(
        Guid id,
        UpsertKnowledgeBaseArticleRequest request,
        CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (article is null)
        {
            return ApplicationResult<KnowledgeBaseArticleResponse>.Failure(ApplicationError.NotFound, "Article not found.");
        }

        var validation = await ValidateArticleRequestAsync(request, cancellationToken);
        if (!validation.IsSuccess)
        {
            return ApplicationResult<KnowledgeBaseArticleResponse>.Failure(validation.Error, validation.Message!);
        }

        ApplyArticleRequest(article, request);
        article.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);

        var updated = await LoadArticleAsync(id, cancellationToken);
        return ApplicationResult<KnowledgeBaseArticleResponse>.Success(ToArticleResponse(updated!));
    }

    private TimeSpan KnowledgeBaseCacheExpiration =>
        TimeSpan.FromSeconds(NormalizeCacheSeconds(cacheOptions.Value.KnowledgeBaseExpirationSeconds, 300));

    private async Task<ApplicationResult> ValidateArticleRequestAsync(
        UpsertKnowledgeBaseArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ApplicationResult.Failure(ApplicationError.Validation, "Article title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            return ApplicationResult.Failure(ApplicationError.Validation, "Article slug is required.");
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken))
        {
            return ApplicationResult.Failure(
                ApplicationError.Validation,
                "Knowledge base category does not exist.");
        }

        return ApplicationResult.Success();
    }

    private static void ApplyArticleRequest(KnowledgeBaseArticle article, UpsertKnowledgeBaseArticleRequest request)
    {
        article.CategoryId = request.CategoryId;
        article.Title = request.Title.Trim();
        article.Slug = NormalizeSlug(request.Slug);
        article.Body = request.Body;
        article.IsPublished = request.IsPublished;
    }

    private async Task<KnowledgeBaseArticle?> LoadArticleAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.KnowledgeBaseArticles
            .AsNoTracking()
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static int NormalizeCacheSeconds(int value, int fallback) => value <= 0 ? fallback : value;

    private static KnowledgeBaseArticleResponse ToArticleResponse(KnowledgeBaseArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Body,
            article.CategoryId,
            article.Category.Name,
            article.IsPublished,
            article.CreatedAt,
            article.UpdatedAt);
}
