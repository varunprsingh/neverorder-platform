import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, formatCurrency, jsonBody } from '../api/client';
import { productImage } from '../api/productImage';
import type { ProductDetail as ProductDetailDto } from '../api/types';
import { useAuth } from '../auth/AuthContext';

export function ProductDetail() {
  const { id } = useParams<{ id: string }>();
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const [quantity, setQuantity] = useState(1);

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['product', id],
    queryFn: () => api<ProductDetailDto>(`/api/products/${id}`),
    enabled: Boolean(id),
  });

  const addToCart = useMutation({
    mutationFn: () =>
      api('/api/cart/items', { method: 'POST', body: jsonBody({ productId: id, quantity }) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
  });

  if (isPending) return <p className="muted">Loading…</p>;
  if (isError) return <p className="error">{(error as Error).message}</p>;

  return (
    <section className="detail">
      <img src={productImage(data.name, data.imageUrl)} alt={data.name} width={600} height={600} />
      <div>
        <span className="pill">{data.categoryName}</span>
        <h1>{data.name}</h1>
        <p className="price large">{formatCurrency(data.price)}</p>
        <p>{data.description}</p>
        <p className="muted">
          {data.inStock ? `${data.availableQuantity} available` : 'Currently out of stock'}
        </p>

        {isAuthenticated ? (
          <div className="add-row">
            <input
              type="number"
              min={1}
              max={Math.max(data.availableQuantity, 1)}
              value={quantity}
              onChange={(e) => setQuantity(Math.max(1, Number(e.target.value)))}
            />
            <button type="button" disabled={!data.inStock || addToCart.isPending} onClick={() => addToCart.mutate()}>
              Add to cart
            </button>
          </div>
        ) : (
          <Link className="button-like" to="/login">
            Sign in to add to cart
          </Link>
        )}

        {addToCart.isError && <p className="error">{(addToCart.error as Error).message}</p>}
        {addToCart.isSuccess && <p className="success">Added to your cart.</p>}
      </div>
    </section>
  );
}
