import { StrictMode, type ReactElement, type ReactNode } from 'react';
import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { AuthContext, type AuthContextValue } from '../auth/AuthContext';
import { ToastProvider } from '../components/ui/ToastProvider';
import type { UserSummary } from '../api/types';

export function makeTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

interface Options extends Omit<RenderOptions, 'wrapper'> {
  route?: string;
  /** Route pattern, when the component under test reads params (e.g. `/products/:id`). */
  path?: string;
  queryClient?: QueryClient;
  /**
   * Skips the real boot sequence and hands the tree a settled session. Pass `null` for an
   * anonymous visitor.
   */
  user?: UserSummary | null;
  /**
   * Wraps in `<StrictMode>`, which is what `main.tsx` does. Effects then run twice in
   * development, and the boot sequence has to survive that.
   */
  strict?: boolean;
}

/** Renders with the real AuthProvider, so the §2.7 boot sequence runs as it would in the app. */
export function renderApp(ui: ReactElement, options: Options = {}) {
  const { route = '/', path, queryClient = makeTestQueryClient(), strict = false, ...rest } = options;

  function Wrapper({ children }: { children: ReactNode }) {
    const tree = (
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <MemoryRouter initialEntries={[route]}>
            <ToastProvider>
              {path ? (
                <Routes>
                  <Route path={path} element={children} />
                </Routes>
              ) : (
                children
              )}
            </ToastProvider>
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>
    );

    return strict ? <StrictMode>{tree}</StrictMode> : tree;
  }

  return { queryClient, ...render(ui, { wrapper: Wrapper, ...rest }) };
}

/** Renders with a pre-settled auth context — for tests about what a role may see, not about boot. */
export function renderWithUser(ui: ReactElement, options: Options = {}) {
  const { route = '/', path, queryClient = makeTestQueryClient(), user = null, ...rest } = options;

  const auth: AuthContextValue = {
    user,
    isBooting: false,
    signIn: async () => {
      throw new Error('not implemented in this harness');
    },
    signOut: async () => {},
    endSession: () => {},
  };

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <AuthContext.Provider value={auth}>
          <MemoryRouter initialEntries={[route]}>
            <ToastProvider>
              {path ? (
                <Routes>
                  <Route path={path} element={children} />
                </Routes>
              ) : (
                children
              )}
            </ToastProvider>
          </MemoryRouter>
        </AuthContext.Provider>
      </QueryClientProvider>
    );
  }

  return { queryClient, ...render(ui, { wrapper: Wrapper, ...rest }) };
}

export const ADMIN: UserSummary = {
  id: '0198c4a2-9b1e-7f04-a6d3-5c8e1b2f7a90',
  email: 'admin@demo.local',
  role: 'Admin',
};

export const STANDARD_USER: UserSummary = {
  id: '0198c3f1-4a2b-7c3d-8e9f-0a1b2c3d4e5f',
  email: 'user@demo.local',
  role: 'User',
};
