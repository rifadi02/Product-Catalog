/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    // The API allowlists exactly http://localhost:5173 (docker-compose.yml -> Cors__AllowedOrigins__0).
    // Without strictPort, a busy 5173 silently falls back to 5174 and every request dies in
    // preflight with an error that looks nothing like "wrong port".
    port: 5173,
    strictPort: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
    // Well above the 5s default. None of these tests are logically slow — the page-level ones
    // drive a real form through React Hook Form and MSW, which takes a couple of seconds on a
    // loaded machine and occasionally crossed 5s, failing for lack of CPU rather than a defect.
    testTimeout: 20_000,
    hookTimeout: 20_000,
    // Pinned rather than inherited from .env, so the MSW handlers' absolute URLs cannot drift
    // away from the client's baseURL.
    env: { VITE_API_BASE_URL: 'http://localhost:8080/api/v1' },
  },
});
