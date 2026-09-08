import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { BASE_URL, getAccessToken } from './client';

export const ORDER_HUB_PATH = '/hubs/orders';
export const ORDER_STATUS_CHANGED = 'orderStatusChanged';

export interface OrderStatusNotification {
  orderId: string;
  orderNumber: string;
  fromStatus: string;
  status: string;
  occurredAt: string;
  isComplete: boolean;
}

export function createOrderHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${BASE_URL}${ORDER_HUB_PATH}`, {
      // Re-read on every (re)connect, so a refreshed token is picked up without rebuilding.
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
