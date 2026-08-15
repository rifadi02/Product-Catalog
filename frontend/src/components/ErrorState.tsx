import { describeError } from '../api/problem';
import { Alert } from './ui/Alert';
import { Button } from './ui/Button';

/**
 * The one place an unexpected API error becomes UI. `describeError` decides what is safe to
 * render — a 500's `detail` is a .NET stack trace in Development and never reaches this (§1.2).
 */
export function ErrorState({
  error,
  onRetry,
  fallback,
  className,
}: {
  error: unknown;
  onRetry?: () => void;
  fallback?: string;
  className?: string;
}) {
  const info = describeError(error, fallback);
  if (info.silent) return null;

  return (
    <Alert
      tone="error"
      title={info.offline ? 'Connection problem' : 'Something went wrong'}
      traceId={info.traceId}
      className={className}
      action={
        onRetry && (
          <Button variant="secondary" size="sm" onClick={onRetry}>
            Retry
          </Button>
        )
      }
    >
      {info.message}
    </Alert>
  );
}
