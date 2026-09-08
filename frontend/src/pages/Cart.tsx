import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, formatCurrency, jsonBody } from '../api/client';
import { productImage } from '../api/productImage';
import type { Cart as CartDto, OrderPlaced } from '../api/types';

export function Cart() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['cart'],
    queryFn: () => api<CartDto>('/api/cart'),
  });

  const refreshCart = () => queryClient.invalidateQueries({ queryKey: ['cart'] });

  const updateQuantity = useMutation({
    mutationFn: ({ productId, quantity }: { productId: string; quantity: number }) =>
      api(`/api/cart/items/${productId}`, { method: 'PUT', body: jsonBody({ quantity }) }),
    onSuccess: refreshCart,
  });

  const removeItem = useMutation({
    mutationFn: (productId: string) => api(`/api/cart/items/${productId}`, { method: 'DELETE' }),
    onSuccess: refreshCart,
  });

  const checkout = useMutation({
    mutationFn: () => api<OrderPlaced>('/api/orders', { method: 'POST' }),
    onSuccess: (order) => {
      refreshCart();
      navigate(`/orders/${order.id}`);
    },
  });

  if (isPending) return <p className="muted">Loading your cart…</p>;
  if (isError) return <p className="error">{(error as Error).message}</p>;

  if (data.items.length === 0) {
    return (
      <section className="empty-state">
        <h1>Your cart is empty</h1>
        <p className="muted">Nothing costs anything here, so feel free to fill it up.</p>
        <Link className="button-like" to="/">
          Browse products
        </Link>
      </section>
    );
  }

  return (
    <section>
      <div className="page-head">
        <h1>Your virtual cart</h1>
      </div>

      <table className="cart-table">
        <thead>
          <tr>
            <th>Item</th>
            <th>Price</th>
            <th>Quantity</th>
            <th>Line total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {data.items.map((item) => (
            <tr key={item.productId} className={item.isAvailable ? '' : 'unavailable'}>
              <td className="item-cell">
                <img src={productImage(item.productName, item.imageUrl)} alt="" />
                <div>
                  <strong>{item.productName}</strong>
                  {!item.isAvailable && <p className="error small">No longer available</p>}
                </div>
              </td>
              <td>{formatCurrency(item.unitPrice)}</td>
              <td>
                <input
                  type="number"
                  min={1}
                  value={item.quantity}
                  onChange={(e) =>
                    updateQuantity.mutate({
                      productId: item.productId,
                      quantity: Math.max(1, Number(e.target.value)),
                    })
                  }
                />
              </td>
              <td>{formatCurrency(item.lineTotal)}</td>
              <td>
                <button type="button" className="link" onClick={() => removeItem.mutate(item.productId)}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="summary">
        <div className="row">
          <span>Subtotal</span>
          <span>{formatCurrency(data.subtotal)}</span>
        </div>
        <div className="row">
          <span>Virtual delivery</span>
          <span>{formatCurrency(data.deliveryFee)}</span>
        </div>
        <div className="row total">
          <span>Total virtual spend</span>
          <span>{formatCurrency(data.total)}</span>
        </div>

        <p className="notice">No payment will be charged. This is a simulated order.</p>

        <button type="button" className="primary" disabled={checkout.isPending} onClick={() => checkout.mutate()}>
          {checkout.isPending ? 'Placing virtual order…' : 'Place virtual order'}
        </button>

        {checkout.isError && <p className="error">{(checkout.error as Error).message}</p>}
      </div>
    </section>
  );
}
