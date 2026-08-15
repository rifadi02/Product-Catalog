import { setupServer } from 'msw/node';

/** One MSW server for the whole suite; handlers are added per test. */
export const server = setupServer();

export const API = 'http://localhost:8080/api/v1';
