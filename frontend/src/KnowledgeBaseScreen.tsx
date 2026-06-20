import { useEffect, useState, type FormEvent } from "react";
import {
  createAdminKnowledgeBaseArticle,
  createAdminKnowledgeBaseCategory,
  getAdminKnowledgeBaseArticle,
  getAdminKnowledgeBaseArticles,
  getAdminKnowledgeBaseCategories,
  getKnowledgeBaseArticle,
  getKnowledgeBaseArticles,
  getKnowledgeBaseCategories,
  updateAdminKnowledgeBaseArticle,
  updateAdminKnowledgeBaseCategory
} from "./api";
import { formatDate } from "./ticketFormat";
import type {
  KnowledgeBaseArticle,
  KnowledgeBaseArticleListItem,
  KnowledgeBaseArticleQuery,
  KnowledgeBaseCategory,
  UpsertKnowledgeBaseArticleInput,
  UpsertKnowledgeBaseCategoryInput,
  UserProfile
} from "./types";

const emptyArticleInput: UpsertKnowledgeBaseArticleInput = {
  categoryId: "",
  title: "",
  slug: "",
  body: "",
  isPublished: false
};

export function KnowledgeBaseScreen({ token, user }: { token: string; user: UserProfile }) {
  const canManage = user.roles.some((role) => role === "Admin" || role === "Agent");
  const [mode, setMode] = useState<"browse" | "manage">("browse");
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [articles, setArticles] = useState<KnowledgeBaseArticleListItem[]>([]);
  const [selectedArticle, setSelectedArticle] = useState<KnowledgeBaseArticle | null>(null);
  const [publicQuery, setPublicQuery] = useState<KnowledgeBaseArticleQuery>({});
  const [adminCategories, setAdminCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [adminArticles, setAdminArticles] = useState<KnowledgeBaseArticleListItem[]>([]);
  const [adminQuery, setAdminQuery] = useState<KnowledgeBaseArticleQuery>({ published: "" });
  const [editorArticleId, setEditorArticleId] = useState<string | null>(null);
  const [articleInput, setArticleInput] = useState<UpsertKnowledgeBaseArticleInput>(emptyArticleInput);
  const [categoryInput, setCategoryInput] = useState<UpsertKnowledgeBaseCategoryInput>({ name: "", description: "" });
  const [editingCategoryId, setEditingCategoryId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadPublic(nextQuery = publicQuery) {
    setIsLoading(true);
    setError(null);

    try {
      const [nextCategories, nextArticles] = await Promise.all([
        getKnowledgeBaseCategories(),
        getKnowledgeBaseArticles(nextQuery)
      ]);
      setCategories(nextCategories);
      setArticles(nextArticles);

      if (nextArticles[0] && !selectedArticle) {
        await selectPublicArticle(nextArticles[0].slug);
      } else if (selectedArticle && !nextArticles.some((article) => article.slug === selectedArticle.slug)) {
        setSelectedArticle(null);
      }
    } catch (err) {
      setError(readError(err, "Failed to load knowledge base."));
    } finally {
      setIsLoading(false);
    }
  }

  async function loadAdmin(nextQuery = adminQuery) {
    if (!canManage) {
      return;
    }

    try {
      const [nextCategories, nextArticles] = await Promise.all([
        getAdminKnowledgeBaseCategories(token),
        getAdminKnowledgeBaseArticles(token, nextQuery)
      ]);
      setAdminCategories(nextCategories);
      setAdminArticles(nextArticles);
      setArticleInput((current) => ({
        ...current,
        categoryId: current.categoryId || nextCategories[0]?.id || ""
      }));
    } catch (err) {
      setError(readError(err, "Failed to load knowledge base admin data."));
    }
  }

  useEffect(() => {
    loadPublic();
  }, []);

  useEffect(() => {
    loadAdmin();
  }, [canManage, token]);

  async function refreshAll() {
    await Promise.all([loadPublic(publicQuery), loadAdmin(adminQuery)]);
  }

  async function selectPublicArticle(slug: string) {
    try {
      setSelectedArticle(await getKnowledgeBaseArticle(slug));
    } catch (err) {
      setError(readError(err, "Failed to load article."));
    }
  }

  async function selectAdminArticle(articleId: string) {
    setError(null);
    const article = await getAdminKnowledgeBaseArticle(token, articleId);
    setEditorArticleId(article.id);
    setArticleInput({
      categoryId: article.categoryId,
      title: article.title,
      slug: article.slug,
      body: article.body,
      isPublished: article.isPublished
    });
  }

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);

    try {
      if (editingCategoryId) {
        await updateAdminKnowledgeBaseCategory(token, editingCategoryId, categoryInput);
      } else {
        await createAdminKnowledgeBaseCategory(token, categoryInput);
      }

      setEditingCategoryId(null);
      setCategoryInput({ name: "", description: "" });
      await refreshAll();
    } catch (err) {
      setError(readError(err, "Failed to save category."));
    } finally {
      setIsSaving(false);
    }
  }

  async function saveArticle(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);

    try {
      const saved = editorArticleId
        ? await updateAdminKnowledgeBaseArticle(token, editorArticleId, articleInput)
        : await createAdminKnowledgeBaseArticle(token, articleInput);

      setEditorArticleId(saved.id);
      setArticleInput({
        categoryId: saved.categoryId,
        title: saved.title,
        slug: saved.slug,
        body: saved.body,
        isPublished: saved.isPublished
      });
      await refreshAll();
      if (saved.isPublished) {
        await selectPublicArticle(saved.slug);
      }
    } catch (err) {
      setError(readError(err, "Failed to save article."));
    } finally {
      setIsSaving(false);
    }
  }

  function editCategory(category: KnowledgeBaseCategory) {
    setEditingCategoryId(category.id);
    setCategoryInput({ name: category.name, description: category.description ?? "" });
  }

  function newArticle() {
    setEditorArticleId(null);
    setArticleInput({ ...emptyArticleInput, categoryId: adminCategories[0]?.id ?? "" });
  }

  function patchPublicQuery(patch: Partial<KnowledgeBaseArticleQuery>) {
    const nextQuery = { ...publicQuery, ...patch };
    setPublicQuery(nextQuery);
    return nextQuery;
  }

  function patchAdminQuery(patch: Partial<KnowledgeBaseArticleQuery>) {
    const nextQuery = { ...adminQuery, ...patch };
    setAdminQuery(nextQuery);
    return nextQuery;
  }

  return (
    <section className="kb-shell">
      {error ? <div className="notice notice-error">{error}</div> : null}

      <header className="kb-hero">
        <div>
          <span className="eyebrow">Self-service support</span>
          <h2>Answers that reduce queue pressure.</h2>
          <p>Search published FAQ articles or maintain drafts from the support-staff admin panel.</p>
        </div>
        <div className="kb-stats">
          <strong>{articles.length}</strong>
          <span>published results</span>
        </div>
      </header>

      <div className="kb-mode-switch" role="tablist" aria-label="Knowledge base mode">
        <button
          className={mode === "browse" ? "kb-mode-active" : ""}
          type="button"
          onClick={() => setMode("browse")}
        >
          Browse
        </button>
        {canManage ? (
          <button
            className={mode === "manage" ? "kb-mode-active" : ""}
            type="button"
            onClick={() => setMode("manage")}
          >
            Manage
          </button>
        ) : null}
      </div>

      {mode === "browse" ? (
        <section className="kb-layout">
          <aside className="kb-sidebar">
            <form
              className="kb-search"
              onSubmit={(event) => {
                event.preventDefault();
                loadPublic(publicQuery);
              }}
            >
              <label>
                Search articles
                <input
                  placeholder="Password reset, SLA, attachments..."
                  value={publicQuery.search ?? ""}
                  onChange={(event) => patchPublicQuery({ search: event.target.value })}
                />
              </label>
              <label>
                Category
                <select
                  value={publicQuery.categoryId ?? ""}
                  onChange={(event) => patchPublicQuery({ categoryId: event.target.value })}
                >
                  <option value="">All categories</option>
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </label>
              <button type="submit">Search FAQ</button>
            </form>

            <div className="kb-category-list">
              {categories.map((category) => (
                <button
                  key={category.id}
                  type="button"
                  onClick={() => loadPublic(patchPublicQuery({ categoryId: category.id }))}
                >
                  <span>{category.name}</span>
                  <small>{category.articleCount} published</small>
                </button>
              ))}
            </div>
          </aside>

          <main className="kb-content">
            <ArticleList
              articles={articles}
              isLoading={isLoading}
              selectedSlug={selectedArticle?.slug ?? null}
              onSelect={selectPublicArticle}
            />
            <ArticleReader article={selectedArticle} />
          </main>
        </section>
      ) : null}

      {mode === "manage" && canManage ? (
        <section className="kb-admin">
          <header>
            <div>
              <span className="eyebrow">Support-staff admin</span>
              <h2>Knowledge base publishing desk</h2>
            </div>
            <button type="button" onClick={newArticle}>
              New article
            </button>
          </header>

          <div className="kb-admin-grid">
            <section className="kb-admin-panel">
              <h3>Categories</h3>
              <form className="kb-form" onSubmit={saveCategory}>
                <label>
                  Name
                  <input
                    value={categoryInput.name}
                    onChange={(event) => setCategoryInput((current) => ({ ...current, name: event.target.value }))}
                    required
                  />
                </label>
                <label>
                  Description
                  <textarea
                    value={categoryInput.description ?? ""}
                    onChange={(event) =>
                      setCategoryInput((current) => ({ ...current, description: event.target.value }))
                    }
                  />
                </label>
                <button type="submit" disabled={isSaving}>
                  {editingCategoryId ? "Update category" : "Create category"}
                </button>
              </form>
              <div className="kb-admin-list">
                {adminCategories.map((category) => (
                  <button key={category.id} type="button" onClick={() => editCategory(category)}>
                    <span>{category.name}</span>
                    <small>{category.articleCount} total articles</small>
                  </button>
                ))}
              </div>
            </section>

            <section className="kb-admin-panel">
              <h3>Article inventory</h3>
              <form
                className="kb-filter-row"
                onSubmit={(event) => {
                  event.preventDefault();
                  loadAdmin(adminQuery);
                }}
              >
                <input
                  placeholder="Search all articles"
                  value={adminQuery.search ?? ""}
                  onChange={(event) => patchAdminQuery({ search: event.target.value })}
                />
                <select
                  value={adminQuery.published === "" || adminQuery.published === undefined ? "" : String(adminQuery.published)}
                  onChange={(event) => patchAdminQuery({ published: parsePublishedFilter(event.target.value) })}
                >
                  <option value="">All</option>
                  <option value="true">Published</option>
                  <option value="false">Drafts</option>
                </select>
                <button type="submit">Apply</button>
              </form>
              <div className="kb-admin-list">
                {adminArticles.map((article) => (
                  <button key={article.id} type="button" onClick={() => selectAdminArticle(article.id)}>
                    <span>{article.title}</span>
                    <small>{article.isPublished ? "Published" : "Draft"} / {article.category}</small>
                  </button>
                ))}
              </div>
            </section>

            <section className="kb-admin-panel kb-editor-panel">
              <h3>{editorArticleId ? "Edit article" : "New article"}</h3>
              <form className="kb-form" onSubmit={saveArticle}>
                <label>
                  Title
                  <input
                    value={articleInput.title}
                    onChange={(event) => {
                      const title = event.target.value;
                      setArticleInput((current) => ({
                        ...current,
                        title,
                        slug: current.slug ? current.slug : toSlug(title)
                      }));
                    }}
                    required
                  />
                </label>
                <label>
                  Slug
                  <input
                    value={articleInput.slug}
                    onChange={(event) => setArticleInput((current) => ({ ...current, slug: toSlug(event.target.value) }))}
                    required
                  />
                </label>
                <label>
                  Category
                  <select
                    value={articleInput.categoryId}
                    onChange={(event) => setArticleInput((current) => ({ ...current, categoryId: event.target.value }))}
                    required
                  >
                    <option value="">Select category</option>
                    {adminCategories.map((category) => (
                      <option key={category.id} value={category.id}>
                        {category.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Body
                  <textarea
                    className="kb-body-input"
                    value={articleInput.body}
                    onChange={(event) => setArticleInput((current) => ({ ...current, body: event.target.value }))}
                    required
                  />
                </label>
                <label className="kb-checkbox">
                  <input
                    checked={articleInput.isPublished}
                    type="checkbox"
                    onChange={(event) =>
                      setArticleInput((current) => ({ ...current, isPublished: event.target.checked }))
                    }
                  />
                  Publish article
                </label>
                <button type="submit" disabled={isSaving || !articleInput.categoryId}>
                  {isSaving ? "Saving..." : "Save article"}
                </button>
              </form>
            </section>
          </div>
        </section>
      ) : null}
    </section>
  );
}

function ArticleList({
  articles,
  isLoading,
  selectedSlug,
  onSelect
}: {
  articles: KnowledgeBaseArticleListItem[];
  isLoading: boolean;
  selectedSlug: string | null;
  onSelect: (slug: string) => void;
}) {
  if (isLoading) {
    return <div className="empty-state">Loading knowledge base...</div>;
  }

  if (articles.length === 0) {
    return <div className="empty-state">No published articles match current filters.</div>;
  }

  return (
    <section className="kb-article-list">
      {articles.map((article) => (
        <button
          key={article.id}
          className={article.slug === selectedSlug ? "kb-article-card kb-article-card-active" : "kb-article-card"}
          type="button"
          onClick={() => onSelect(article.slug)}
        >
          <span>{article.category}</span>
          <strong>{article.title}</strong>
          <small>Updated {formatDate(article.updatedAt)}</small>
        </button>
      ))}
    </section>
  );
}

function ArticleReader({ article }: { article: KnowledgeBaseArticle | null }) {
  if (!article) {
    return (
      <article className="kb-reader">
        <p className="empty-state">Select an article to read the answer.</p>
      </article>
    );
  }

  return (
    <article className="kb-reader">
      <span className="eyebrow">{article.category}</span>
      <h2>{article.title}</h2>
      <small>Updated {formatDate(article.updatedAt)}</small>
      <div className="kb-body">
        {article.body.split(/\n{2,}/).map((paragraph) => (
          <p key={paragraph}>{paragraph}</p>
        ))}
      </div>
    </article>
  );
}

function parsePublishedFilter(value: string): boolean | "" {
  if (value === "") {
    return "";
  }

  return value === "true";
}

function toSlug(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function readError(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}
