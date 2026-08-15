/** Register — PDR §3.3, driving `POST /api/v1/auth/register` (§2.4). */
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { register as registerRequest } from '../api/auth';
import { applyServerErrors, describeError, getProblem, retryAfterSeconds } from '../api/problem';
import { PASSWORD_RULES, registerSchema, type RegisterFormValues } from '../lib/schemas';
import { useCountdown } from '../hooks/useCountdown';
import { Alert } from '../components/ui/Alert';
import { Button } from '../components/ui/Button';
import { TextInput } from '../components/ui/Field';
import { PasswordInput } from '../components/ui/PasswordInput';

export function RegisterPage() {
  const navigate = useNavigate();
  const countdown = useCountdown();
  const [formError, setFormError] = useState<{ message: string; traceId?: string } | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    mode: 'onBlur',
    defaultValues: { email: '', password: '', confirmPassword: '' },
  });

  const password = watch('password');

  async function onSubmit(values: RegisterFormValues) {
    setFormError(null);

    try {
      await registerRequest(values);

      // 201 carries no tokens — the user is not logged in (§2.4).
      navigate('/login?registered=1', { replace: true });
    } catch (error) {
      const info = describeError(error, "Can't reach the server. Try again.");

      // There is no `errors` bag on a 409, so this code maps onto the field manually (§3.3).
      if (info.code === 'email_already_registered') {
        setError('email', { type: 'server', message: info.message });
        return;
      }

      if (applyServerErrors(getProblem(error), setError)) return;

      if (info.code === 'rate_limited') {
        // 5 per 15 minutes — tight, and easy to trip while testing.
        countdown.start(retryAfterSeconds(error) ?? 900);
      }

      setFormError({ message: info.message, traceId: info.traceId });
    }
  }

  return (
    <div className="mx-auto max-w-[420px]">
      <div className="card p-6">
        <h1 className="text-xl font-semibold">Create an account</h1>

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
            autoComplete="new-password"
            required
            error={errors.password?.message}
            {...register('password')}
          />

          {/*
            The backend's password rules chain, so one weak password can return four separate
            messages at once. Showing the rules up front avoids that wall of text (§3.3).
          */}
          <ul className="space-y-1 text-xs">
            {PASSWORD_RULES.map((rule) => {
              const met = rule.test(password ?? '');
              return (
                <li
                  key={rule.label}
                  className={met ? 'text-emerald-700' : 'text-ink-500'}
                >
                  <span aria-hidden="true">{met ? '✓' : '○'}</span>{' '}
                  <span className="sr-only">{met ? 'Met:' : 'Not met:'}</span>
                  {rule.label}
                </li>
              );
            })}
          </ul>

          <PasswordInput
            label="Confirm password"
            autoComplete="new-password"
            required
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />

          <Button
            type="submit"
            className="w-full"
            isPending={isSubmitting}
            disabled={countdown.isActive}
          >
            {countdown.isActive ? `Wait ${countdown.remaining}s` : 'Create account'}
          </Button>
        </form>

        <p className="mt-4 text-sm text-ink-500">
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-brand-700 hover:underline">
            Sign in
          </Link>
        </p>
      </div>
    </div>
  );
}
