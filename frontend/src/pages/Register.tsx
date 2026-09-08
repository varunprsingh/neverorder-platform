import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export function Register() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  return (
    <section className="auth-card">
      <h1>Create your account</h1>
      <p className="muted">Only needed so your virtual cart and orders stay yours.</p>

      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setErrors([]);
          setBusy(true);
          try {
            await register(name, email, password);
            navigate('/');
          } catch (err) {
            if (err instanceof ApiError && err.fieldErrors) {
              setErrors(Object.values(err.fieldErrors).flat());
            } else {
              setErrors([(err as Error).message]);
            }
          } finally {
            setBusy(false);
          }
        }}
      >
        <label>
          Name
          <input required value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label>
          Email
          <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label>
          Password
          <input
            type="password"
            required
            minLength={8}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <small className="muted">
            At least 8 characters, with an uppercase letter, a lowercase letter and a digit.
          </small>
        </label>

        {errors.length > 0 && (
          <ul className="error">
            {errors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        )}

        <button type="submit" className="primary" disabled={busy}>
          {busy ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <p className="muted">
        Already registered? <Link to="/login">Sign in</Link>.
      </p>
    </section>
  );
}
