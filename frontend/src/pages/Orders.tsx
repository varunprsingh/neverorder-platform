import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api, formatCurrency } from '../api/client';
import type { OrderSummary, PagedResult } from '../api/types';

export function Orders() {
  const [page, setPage] = useState(1);

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['orders', page],
    queryFn: () => api<PagedResult<OrderSummary>>(`/api/orders?page=${page}&pageSize=10`),
    // Keep the list fresh while orders are still moving through the simulation.
    refetchInterval: 3000,
  });

  if (isPending) return <p className="muted">Loading your orders…</p>;
  if (isError) return <p className="error">{(error as Error).message}</p>;

  if (data.items.length === 0) {
    return (
      <section className="empty-state">
        <h1>No virtual orders yet</h1>
        <Link className="button-like" to="/">
          Start browsing
        </Link>
      </section>
    );
  }

  const totalSpend = data.items.reduce((sum, order) => sum + order.total, 0);

  return (
    <section>
      <div className="page-head">
        <h1>Order history</h1>
        <p className="muted">
          {data.totalCount} virtual orders · {formatCurrency(totalSpend)} of simulated spending on this
          page
        </p>
      </div>

      <table className="cart-table">
        <thead>
          <tr>
            <th>Order</th>
            <th>Placed</th>
            <th>Items</th>
            <th>Total</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((order) => (
            <tr key={order.id}>
              <td>
                <Link to={`/orders/${order.id}`}>{order.orderNumber}</Link>
              </td>
              <td>{new Date(order.createdAt).toLocaleString()}</td>
              <td>{order.itemCount}</td>
              <td>{formatCurrency(order.total)}</td>
              <td>
                <span className={`status status-${order.status.toLowerCase()}`}>{order.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {data.totalPages > 1 && (
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
