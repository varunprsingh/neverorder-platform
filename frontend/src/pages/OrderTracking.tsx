import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api, formatCurrency } from '../api/client';
import { useOrderStream } from '../hooks/useOrderStream';
import type { OrderDetail as OrderDetailDto, OrderTracking as OrderTrackingDto } from '../api/types';

export function OrderTracking() {
  const { id } = useParams<{ id: string }>();
  const isLive = useOrderStream(id);

  const { data: tracking } = useQuery({
    queryKey: ['tracking', id],
    queryFn: () => api<OrderTrackingDto>(`/api/orders/${id}/tracking`),
    enabled: Boolean(id),
    // The hub pushes updates; polling is the fallback for when it cannot connect.
    refetchInterval: (query) => {
      if (query.state.data?.isComplete) return false;
      return isLive ? false : 1000;
    },
  });

  const { data: order, isPending, isError, error } = useQuery({
    queryKey: ['order', id, tracking?.status],
    queryFn: () => api<OrderDetailDto>(`/api/orders/${id}`),
    enabled: Boolean(id),
  });

  if (isPending) return <p className="muted">Loading your order…</p>;
  if (isError) return <p className="error">{(error as Error).message}</p>;

  return (
    <section>
      <div className="page-head">
        <h1>Order {order.orderNumber}</h1>
        <p className="muted">
          Placed {new Date(order.createdAt).toLocaleString()} · {formatCurrency(order.total)} virtual
          spend
        </p>
      </div>

      <div className="tracker">
        {tracking?.steps.map((step) => (
          <div
            key={step.status}
            className={`step ${step.isReached ? 'reached' : ''} ${step.isCurrent ? 'current' : ''}`}
          >
            <span className="marker" aria-hidden="true" />
            <div>
              <strong>{step.label}</strong>
              {step.occurredAt && (
                <p className="muted small">{new Date(step.occurredAt).toLocaleTimeString()}</p>
              )}
            </div>
          </div>
        ))}
      </div>

      {tracking && !tracking.isComplete && (
        <p className="muted">
          {isLive
            ? 'Live — updates are pushed as the backend advances the order…'
            : 'Status updates automatically as the backend advances the order…'}
        </p>
      )}
      {tracking?.isComplete && <p className="success">Virtually delivered. Nothing was charged.</p>}

      <h2>Items</h2>
      <table className="cart-table">
        <thead>
          <tr>
            <th>Item</th>
            <th>Price</th>
            <th>Qty</th>
            <th>Line total</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.productId}>
              <td>{item.productName}</td>
              <td>{formatCurrency(item.unitPrice)}</td>
              <td>{item.quantity}</td>
              <td>{formatCurrency(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="summary">
        <div className="row">
          <span>Subtotal</span>
          <span>{formatCurrency(order.subtotal)}</span>
        </div>
        <div className="row">
          <span>Virtual delivery</span>
          <span>{formatCurrency(order.deliveryFee)}</span>
        </div>
        <div className="row total">
          <span>Total virtual spend</span>
          <span>{formatCurrency(order.total)}</span>
        </div>
      </div>

      <h2>Status history</h2>
      <ul className="history">
        {order.statusHistory.map((entry, index) => (
          <li key={`${entry.toStatus}-${index}`}>
            <span className="mono">{new Date(entry.occurredAt).toLocaleTimeString()}</span>
            <span>
              {entry.fromStatus ? `${entry.fromStatus} → ${entry.toStatus}` : entry.toStatus}
            </span>
            {entry.note && <span className="muted">{entry.note}</span>}
          </li>
        ))}
      </ul>

      <Link className="button-like" to="/orders">
        Back to order history
      </Link>
    </section>
  );
}
