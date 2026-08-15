import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * The 429 countdown — §1.2, §3.2. Login is limited to 10 requests per 5 minutes and register to
 * 5 per 15 minutes, so this is easy to trip while testing and the user needs to see how long
 * they are locked out for rather than a button that just refuses.
 */
export function useCountdown() {
  const [remaining, setRemaining] = useState(0);
  const deadline = useRef(0);

  const start = useCallback((seconds: number) => {
    deadline.current = Date.now() + seconds * 1000;
    setRemaining(seconds);
  }, []);

  useEffect(() => {
    if (remaining <= 0) return;

    const timer = setInterval(() => {
      // Recomputed from a deadline rather than decremented: an interval in a background tab is
      // throttled, and counting ticks would leave the button disabled long after the limit lifted.
      const left = Math.ceil((deadline.current - Date.now()) / 1000);
      setRemaining(left > 0 ? left : 0);
    }, 250);

    return () => clearInterval(timer);
  }, [remaining]);

  return { remaining, isActive: remaining > 0, start };
}
