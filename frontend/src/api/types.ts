export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export interface Category {
  id: string;
  name: string;
  slug: string;
}

export interface ProductSummary {
  id: string;
  name: string;
  price: number;
  imageUrl: string;
  categoryName: string;
  categorySlug: string;
  availableQuantity: number;
  inStock: boolean;
}

export interface ProductDetail extends ProductSummary {
  description: string;
  categoryId: string;
}

export interface CartItem {
  productId: string;
  productName: string;
  imageUrl: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  isAvailable: boolean;
}

export interface Cart {
  id: string;
  items: CartItem[];
  subtotal: number;
  deliveryFee: number;
  total: number;
  itemCount: number;
}

export interface AuthenticatedUser {
  id: string;
  name: string;
  email: string;
  roles: string[];
}

export interface AuthResult {
  accessToken: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

export interface OrderPlaced {
  id: string;
  orderNumber: string;
  status: string;
  subtotal: number;
  deliveryFee: number;
  total: number;
  createdAt: string;
  notice: string;
}

export interface OrderSummary {
  id: string;
  orderNumber: string;
  createdAt: string;
  status: string;
  total: number;
  itemCount: number;
}

export interface OrderItem {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderStatusHistoryEntry {
  fromStatus: string | null;
  toStatus: string;
  occurredAt: string;
  note: string | null;
}

export interface OrderDetail {
  id: string;
  orderNumber: string;
  createdAt: string;
  updatedAt: string;
  status: string;
  subtotal: number;
  deliveryFee: number;
  total: number;
  items: OrderItem[];
  statusHistory: OrderStatusHistoryEntry[];
}

export interface TrackingStep {
  status: string;
  label: string;
  isReached: boolean;
  isCurrent: boolean;
  occurredAt: string | null;
}

export interface OrderTracking {
  id: string;
  orderNumber: string;
  status: string;
  isComplete: boolean;
  nextTransitionAt: string | null;
  steps: TrackingStep[];
}
