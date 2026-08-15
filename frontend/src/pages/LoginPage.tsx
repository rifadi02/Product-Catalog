/** Login — PDR §3.2, driving `POST /api/v1/auth/login` (§2.3). */
import { useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { applyServerErrors, describeError, getProblem, retryAfterSeconds } from '../api/problem';
import { loginSchema, type LoginFormValues } from '../lib/schemas';
import { DEV_ACCOUNTS } from '../lib/constants';
import { useAuth } from '../auth/useAuth';
import { useCountdown } from '../hooks/useCountdown';
import { Alert } from '../components/ui/Alert';
import { Button } from '../components/ui/Button';
import { TextInput } from '../components/ui/Field';
import { PasswordInput } from '../components/ui/PasswordInput';

interface FromState {
  from?: { pathname: string; search?: string };
}

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const countdown = useCountdown();

  const [formError, setFormError] = useState<{ message: string; traceId?: string } | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onBlur',
    defaultValues: { email: '', password: '' },
  });

  const sessionExpired = searchParams.get('reason') === 'expired';
  const registered = searchParams.get('registered') === '1';

  async function onSubmit(values: LoginFormValues) {
    setFormError(null);

    try {
      await signIn(values);

      const state = location.state as FromState | null;
      const target = state?.from
        ? `${state.from.pathname}${state.from.search ?? ''}`
        : '/products';

      navigate(target, { replace: true });
    } catch (error) {
      const info = describeError(error, "Can't reach the server. Try again.");

      // 400 with an errors bag maps straight onto the fields; everything else is form-level.
      if (applyServerErrors(getProblem(error), setError)) return;

      if (info.code === 'rate_limited') {
        countdown.start(retryAfterSeconds(error) ?? 60);
      }

      // Never say which half was wrong — the server deliberately does not (§3.2).
      setFormError({ message: info.message, traceId: info.traceId });
    }
  }

  return (
    <div className="mx-auto max-w-[420px]">
      <div className="card p-6">
        <h1 className="text-xl font-semibold">Sign in</h1>

        {registered && (
          <Alert tone="success" className="mt-4">
            Account created. Please sign in.
          </Alert>
        )}

        {sessionExpired && !formError && (
          <Alert tone="warning" className="mt-4">
            Your session has ended. Please sign in again.
          </Alert>
        )}

        {formError && (
          <Alert tone="error" className="mt-4" traceId={formError.traceId}>
            {formError.message}
            {countdown.isActive && (
              <span className="mt-1 block tabular-nums">
                Try again in {countdown.remaining}s.
              </span>
            )}
          </Alert>
        )}

        <form className="mt-5 space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <TextInput
            label="Email"
            type="email"
            autoComplete="username"
            autoFocus
            required
            error={errors.email?.message}
            {...register('email')}
          />

          <PasswordInput
            label="Password"
            autoComplete="current-password"
            required
            error={errors.password?.message}
            {...register('password')}
          />

          <Button
            type="submit"
            className="w-full"
            isPending={isSubmitting}
            disabled={countdown.isActive}
          >
            {countdown.isActive ? `Wait ${countdown.remaining}s` : 'Sign in'}
          </Button>
        </form>

        <p className="mt-4 text-sm text-ink-500">
          <Link to="/register" className="font-medium text-brand-700 hover:underline">
            Create an account
          </Link>
        </p>
      </div>

      {/* §0.5 — seeded accounts, development only. */}
      {import.meta.env.DEV && (
        <div className="mt-4 rounded-lg border border-dashed border-slate-300 p-4 text-xs text-ink-500">
          <p className="font-medium">Development accounts</p>
          <ul className="mt-2 space-y-1">
            {DEV_ACCOUNTS.map((account) => (
              <li key={account.email} className="font-mono">
                {account.email} / {account.password}
                <span className="ml-2 font-sans not-italic">({account.role})</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
