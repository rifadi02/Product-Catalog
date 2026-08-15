import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { SESSION_ENDED_EVENT } from '../api/client';
import { useAuth } from './useAuth';

/**
 * The other half of the interceptor's terminal path (§2.6). `client.ts` dispatches a DOM event
 * rather than calling the router, which keeps it free of React imports; this listens once at the
 * app root and turns it into a cache clear and a redirect.
 */
export function SessionEndedListener() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { endSession } = useAuth();

  useEffect(() => {
    function onSessionEnded() {
      endSession();
      queryClient.clear();
      navigate('/login?reason=expired', { replace: true });
    }

    window.addEventListener(SESSION_ENDED_EVENT, onSessionEnded);
    return () => window.removeEventListener(SESSION_ENDED_EVENT, onSessionEnded);
  }, [navigate, queryClient, endSession]);

  return null;
}
