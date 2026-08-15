/**
 * Token storage — PDR §2.2.
 *
 * The access token lives in a module-scoped variable and is never persisted; the refresh token
 * lives in `localStorage` because the backend delivers it in a JSON body rather than an
 * `HttpOnly` cookie, and holding it in memory would log the user out on every page reload.
 *
 * The trade-off is stated plainly in §2.2: `localStorage` is readable by any injected script.
 * It is defensible here because the access token itself is never stored, refresh tokens rotate
 * on every use, and server-side reuse detection kills the whole session if a stolen token is
 * replayed. If the backend ever moves the refresh token to a cookie, only this file changes.
 */

let accessToken: string | null = null;

export const getAccessToken = () => accessToken;
export const setAccessToken = (t: string | null) => {
  accessToken = t;
};

const REFRESH_KEY = 'catalog.refreshToken';

export const getRefreshToken = (): string | null => {
  try {
    return localStorage.getItem(REFRESH_KEY);
  } catch {
    // Private-mode Safari and hardened browser profiles throw on storage access.
    return null;
  }
};

export const setRefreshToken = (t: string | null) => {
  try {
    if (t) localStorage.setItem(REFRESH_KEY, t);
    else localStorage.removeItem(REFRESH_KEY);
  } catch {
    /* nothing useful to do — the session simply will not survive a reload */
  }
};

export function clearTokens() {
  accessToken = null;
  setRefreshToken(null);
}

/**
 * The refresh DTO is validated at 16–512 characters and a malformed value produces a 400, not a
 * 401 (§4.5). A stored token outside that range means storage is corrupt: clear it rather than
 * sending a request the interceptor would misread as an expired session. Real tokens are 43
 * characters of Base64Url.
 */
export function isPlausibleRefreshToken(t: string | null): t is string {
  return typeof t === 'string' && t.length >= 16 && t.length <= 512;
}
