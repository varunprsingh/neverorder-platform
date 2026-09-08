import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { createOrderHubConnection, ORDER_STATUS_CHANGED } from '../api/orderHub';
import type { OrderStatusNotification } from '../api/orderHub';

/**
 * Subscribes to live order updates and refreshes the cached queries when one arrives.
 * Returns whether the stream is actually connected, so a caller can keep polling when it is not:
 * a hub that fails to connect must degrade to the old behaviour, not to a page that never updates.
 */
export function useOrderStream(orderId: string | undefined): boolean {
  const queryClient = useQueryClient();
  const [isLive, setIsLive] = useState(false);

  useEffect(() => {
    if (!orderId) return;

    const connection = createOrderHubConnection();
    let cancelled = false;

    connection.on(ORDER_STATUS_CHANGED, (notification: OrderStatusNotification) => {
      // The hub only ever sends this user's orders; this narrows it to the one on screen.
      if (notification.orderId !== orderId) return;

      void queryClient.invalidateQueries({ queryKey: ['tracking', orderId] });
      void queryClient.invalidateQueries({ queryKey: ['order', orderId] });
    });

    connection.onreconnecting(() => setIsLive(false));
    connection.onreconnected(() => setIsLive(true));
    connection.onclose(() => setIsLive(false));

    const started = connection
      .start()
      .then(() => {
        if (!cancelled) setIsLive(true);
      })
      .catch(() => {
        if (!cancelled) setIsLive(false);
      });

    return () => {
      cancelled = true;
      setIsLive(false);
      // Stopping mid-handshake aborts negotiation and logs an error, which StrictMode's double
      // mount would trigger on every render in development.
      void started.finally(() => connection.stop());
    };
  }, [orderId, queryClient]);

  return isLive;
}
