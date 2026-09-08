import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { Cart } from '../api/types';
import { useAuth } from '../auth/AuthContext';

export function Layout() {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  const { data: cart } = useQuery({
    queryKey: ['cart'],
    queryFn: () => api<Cart>('/api/cart'),
    enabled: isAuthenticated,
  });

  return (
    <div className="app">
      <div className="virtual-banner">
        Virtual store — nothing here is ever really bought. No payment is ever charged.
      </div>

      <header className="site-header">
        <NavLink to="/" className="brand">
          Never<span>Order</span>
          <small>Shop. Order. Track. Never Pay.</small>
        </NavLink>

        <nav>
          <NavLink to="/">Products</NavLink>
          {isAuthenticated && <NavLink to="/orders">Orders</NavLink>}
          {isAuthenticated && (
            <NavLink to="/cart">Cart{cart && cart.itemCount > 0 ? ` (${cart.itemCount})` : ''}</NavLink>
          )}
        </nav>

        <div className="account">
          {isAuthenticated ? (
            <>
              <span className="who">{user?.name}</span>
              <button
                type="button"
                className="link"
                onClick={() => {
                  logout();
                  navigate('/');
                }}
              >
                Sign out
              </button>
            </>
          ) : (
            <>
              <NavLink to="/login">Sign in</NavLink>
              <NavLink to="/register" className="cta">
                Create account
              </NavLink>
            </>
          )}
        </div>
      </header>

      <main>
        <Outlet />
      </main>

      <footer>
        NeverOrder is a simulation built to demonstrate backend engineering. No products ship and no
        money moves.
      </footer>
    </div>
  );
}
