import { Outlet } from 'react-router-dom';
import { NavBar } from './NavBar';

export function AppLayout() {
  return (
    <div className="min-h-dvh">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:m-2 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        Skip to content
      </a>
      <NavBar />
      <main id="main" className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  );
}
