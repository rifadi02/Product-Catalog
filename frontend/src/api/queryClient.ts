import { QueryClient } from '@tanstack/react-query';
import { isProblem } from './types';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: (failureCount, error) => {
          // Retrying a 4xx just repeats a rejected request. 401s are already handled by the
          // interceptor's refresh-and-replay, and a 404 is an answer, not a failure.
          const status = isProblem(error) ? error.response?.status : undefined;
          if (status && status < 500) return false;
          return failureCount < 2;
        },
        refetchOnWindowFocus: false,
      },
      mutations: { retry: false },
    },
  });
}
