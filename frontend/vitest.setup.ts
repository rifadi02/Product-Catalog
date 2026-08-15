import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { server } from './src/test/server';

// Unhandled requests fail loudly: a test that silently talks to a real host is a test that
// passes for the wrong reason.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterAll(() => server.close());

afterEach(() => {
  server.resetHandlers();
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});

beforeEach(() => {
  // jsdom has no crypto.randomUUID in every version we might land on, and the request
  // interceptor stamps every call with an X-Correlation-Id.
  if (!globalThis.crypto?.randomUUID) {
    Object.defineProperty(globalThis, 'crypto', {
      value: { ...globalThis.crypto, randomUUID: () => '00000000-0000-4000-8000-000000000000' },
      configurable: true,
    });
  }
});

// jsdom does not implement these, and Modal/Toast touch both.
if (!window.matchMedia) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
}

if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = vi.fn();
}
