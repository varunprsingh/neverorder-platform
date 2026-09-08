import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, formatCurrency, jsonBody } from '../api/client';
import { productImage } from '../api/productImage';
import type { Category, PagedResult, ProductSummary } from '../api/types';
import { useAuth } from '../auth/AuthContext';

export function Products() {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [category, setCategory] = useState('');
  const [sort, setSort] = useState('');
  const [page, setPage] = useState(1);

  const { data: categories } = useQuery({
    queryKey: ['categories'],
    queryFn: () => api<Category[]>('/api/categories'),
  });

  const params = new URLSearchParams({ page: String(page), pageSize: '12' });
  if (submittedSearch) params.set('search', submittedSearch);
  if (category) params.set('category', category);
  if (sort) params.set('sort', sort);

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['products', params.toString()],
    queryFn: () => api<PagedResult<ProductSummary>>(`/api/products?${params}`),
  });

  const addToCart = useMutation({
    mutationFn: (productId: string) =>
      api('/api/cart/items', { method: 'POST', body: jsonBody({ productId, quantity: 1 }) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
  });

  // One mutation serves every card, so its pending flag has to be narrowed to the card that
  // triggered it — otherwise adding one item greys out the whole grid.
  const addingProductId = addToCart.isPending ? addToCart.variables : undefined;

  return (
    <section>
      <div className="page-head">
        <h1>Browse the catalogue</h1>
        <p className="muted">Add anything you like. You will never be charged for it.</p>
      </div>

      <form
        className="filters"
        onSubmit={(e) => {
          e.preventDefault();
          setPage(1);
          setSubmittedSearch(search);
        }}
      >
        <input
          type="search"
          placeholder="Search products…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          value={category}
          onChange={(e) => {
            setPage(1);
            setCategory(e.target.value);
          }}
        >
          <option value="">All categories</option>
          {categories?.map((c) => (
            <option key={c.id} value={c.slug}>
              {c.name}
            </option>
          ))}
        </select>
        <select
          value={sort}
          onChange={(e) => {
            setPage(1);
            setSort(e.target.value);
          }}
        >
          <option value="">Name (A–Z)</option>
          <option value="price_asc">Price: low to high</option>
          <option value="price_desc">Price: high to low</option>
          <option value="newest">Newest</option>
        </select>
        <button type="submit">Search</button>
      </form>

      {isPending && <p className="muted">Loading products…</p>}
      {isError && <p className="error">{(error as Error).message}</p>}

      {data && data.items.length === 0 && <p className="muted">No products matched that search.</p>}

      <div className="product-grid">
        {data?.items.map((product) => (
          <article key={product.id} className="product-card">
            <Link to={`/products/${product.id}`}>
              <img
                src={productImage(product.name, product.imageUrl)}
                alt={product.name}
                width={600}
                height={600}
                loading="lazy"
              />
            </Link>
            <div className="product-body">
              <span className="pill">{product.categoryName}</span>
              <h3>
                <Link to={`/products/${product.id}`}>{product.name}</Link>
              </h3>
              <p className="price">{formatCurrency(product.price)}</p>
              {isAuthenticated ? (
                <button
                  type="button"
                  disabled={!product.inStock || addingProductId === product.id}
                  onClick={() => addToCart.mutate(product.id)}
                >
                  {!product.inStock
                    ? 'Out of stock'
                    : addingProductId === product.id
                      ? 'Adding…'
                      : 'Add to cart'}
                </button>
              ) : (
                <Link className="button-like" to="/login">
                  Sign in to add
                </Link>
              )}
            </div>
          </article>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="pagination">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </button>
          <span>
            Page {data.page} of {data.totalPages}
          </span>
          <button type="button" disabled={!data.hasNextPage} onClick={() => setPage((p) => p + 1)}>
            Next
          </button>
        </div>
      )}
    </section>
  );
}
