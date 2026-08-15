/** The error contract — PDR §1. */
import { describe, expect, it, vi } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { applyServerErrors, describeError, hasCode, retryAfterSeconds } from './problem';
import { makeProblem } from '../test/factories';
import type { ProblemDetails } from './types';

function axiosErrorFor(problem: ProblemDetails, headers: Record<string, string> = {}) {
  const error = new AxiosError('Request failed', 'ERR_BAD_REQUEST');
  error.response = {
    data: problem,
    status: problem.status,
    statusText: '',
    headers,
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe('applyServerErrors', () => {
  it('maps every key in the errors bag onto a form field', () => {
    const setError = vi.fn();
    const problem = makeProblem('validation_failed', 400, {
      errors: {
        name: ['Name is required.'],
        price: ['Price must be between 0 and 999999.99.'],
      },
    });

    expect(applyServerErrors(problem, setError)).toBe(true);
    expect(setError).toHaveBeenCalledWith('name', {
      type: 'server',
      message: 'Name is required.',
    });
  });

  it('joins the chained password messages into one string', () => {
    const setError = vi.fn();
    // The backend's password rules chain, so one weak password returns several strings under
    // a single key (§1.3).
    const problem = makeProblem('validation_failed', 400, {
      errors: {
        password: [
          'Password must contain at least one uppercase letter.',
          'Password must contain at least one digit.',
        ],
      },
    });

    applyServerErrors(problem, setError);

    expect(setError).toHaveBeenCalledWith('password', {
      type: 'server',
      message:
        'Password must contain at least one uppercase letter. Password must contain at least one digit.',
    });
  });

  it('reports false when there is no errors bag', () => {
    const setError = vi.fn();
    // 409 email_already_registered arrives without one, and must be mapped manually (§2.4).
    expect(applyServerErrors(makeProblem('email_already_registered', 409), setError)).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });
});

describe('describeError', () => {
  it('never renders the detail of a 500', () => {
    // In Development that detail is a full .NET stack trace (§1.1).
    const error = axiosErrorFor(
      makeProblem('internal_error', 500, {
        detail: 'System.NullReferenceException: Object reference not set...',
      }),
    );

    const info = describeError(error);

    expect(info.message).toBe('Something went wrong.');
    expect(info.message).not.toContain('NullReferenceException');
    expect(info.traceId).toBe('0HN7GK3P9QVJ1:00000003');
  });

  it('shows a fixed message for invalid credentials rather than the server detail', () => {
    const info = describeError(axiosErrorFor(makeProblem('invalid_credentials', 401)));
    expect(info.message).toBe('Email or password is incorrect.');
  });

  it('treats 403 as a permission message, not a session problem', () => {
    const info = describeError(axiosErrorFor(makeProblem('insufficient_role', 403)));
    expect(info.message).toBe("You don't have permission to do that.");
    expect(info.code).toBe('insufficient_role');
  });

  it('is silent for our own cancelled requests', () => {
    const info = describeError(axiosErrorFor(makeProblem('request_cancelled', 499)));
    expect(info.silent).toBe(true);
  });

  it('shows the 4xx detail, which is human-readable and safe', () => {
    const error = axiosErrorFor(
      makeProblem('resource_not_found', 404, { detail: 'Product 999 was not found.' }),
    );
    expect(describeError(error).message).toBe('Product 999 was not found.');
  });

  it('flags a missing response as offline rather than unauthenticated', () => {
    // A CORS rejection, DNS failure or dead API produces no problem document at all (§1.5).
    const info = describeError(new AxiosError('Network Error', 'ERR_NETWORK'));

    expect(info.offline).toBe(true);
    expect(info.message).toBe("Can't reach the server. Try again.");
  });
});

describe('retryAfterSeconds', () => {
  it('reads the CORS-exposed Retry-After header', () => {
    const error = axiosErrorFor(makeProblem('rate_limited', 429), { 'retry-after': '231' });
    expect(retryAfterSeconds(error)).toBe(231);
  });

  it('returns undefined when the header is absent', () => {
    expect(retryAfterSeconds(axiosErrorFor(makeProblem('rate_limited', 429)))).toBeUndefined();
  });
});

describe('hasCode', () => {
  it('matches on code, which is the contract', () => {
    const error = axiosErrorFor(makeProblem('concurrency_conflict', 409));
    expect(hasCode(error, 'concurrency_conflict')).toBe(true);
    expect(hasCode(error, 'resource_not_found')).toBe(false);
  });
});
