/** Auth endpoints — PDR §2. */
import { api } from './client';
import type {
  AuthTokens,
  LoginRequest,
  RegisterRequest,
  RegisterResponse,
  UserSummary,
} from './types';

/** §2.3 — the response already contains the full user object; no `/auth/me` round trip. */
export async function login(body: LoginRequest): Promise<AuthTokens> {
  const { data } = await api.post<AuthTokens>('/auth/login', body);
  return data;
}

/** §2.4 — returns no tokens. The user is not logged in afterwards. */
export async function register(body: RegisterRequest): Promise<RegisterResponse> {
  const { data } = await api.post<RegisterResponse>('/auth/register', body);
  return data;
}

/** §2.7 — session rehydration on app boot, not a post-login call. */
export async function getMe(): Promise<UserSummary> {
  const { data } = await api.get<UserSummary>('/auth/me');
  return data;
}

/**
 * §2.8 — requires both the access token and the refresh token in the body. Deliberately
 * forgiving: an unknown token still returns 204, so a 204 is not proof anything was revoked.
 */
export async function logout(refreshToken: string): Promise<void> {
  await api.post('/auth/logout', { refreshToken });
}
