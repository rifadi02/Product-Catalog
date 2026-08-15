/**
 * Probes the API before any spec runs and records whether it is reachable.
 *
 * The specs read `process.env.E2E_API_READY` and skip when it is not `'1'`. Skipping beats
 * failing: these tests need `docker compose up`, and a red suite on a machine without Docker is
 * a red suite people learn to ignore.
 */
const READY_URL = process.env.E2E_HEALTH_URL ?? 'http://localhost:8080/health/ready';

export default async function globalSetup() {
  process.env.E2E_API_READY = '0';

  try {
    const response = await fetch(READY_URL, { signal: AbortSignal.timeout(5000) });

    if (response.ok) {
      process.env.E2E_API_READY = '1';
      console.log(`[e2e] API ready at ${READY_URL}`);
      return;
    }

    console.warn(`[e2e] ${READY_URL} returned ${response.status} — skipping end-to-end specs.`);
  } catch (error) {
    console.warn(
      `[e2e] Could not reach ${READY_URL} (${(error as Error).message}).`,
      '\n[e2e] Start the API with `docker compose up --build` from the repository root.',
      '\n[e2e] Skipping end-to-end specs.',
    );
  }
}
