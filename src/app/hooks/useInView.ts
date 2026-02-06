import { useEffect, useRef, useState } from 'react';

/**
 * Hook to detect when an element enters the viewport
 * Useful for triggering animations on scroll
 * 
 * @param options - IntersectionObserver options
 * @returns { ref, isInView } - ref to attach to element, boolean indicating visibility
 * 
 * @example
 * const { ref, isInView } = useInView({ threshold: 0.5 });
 * return <div ref={ref} className={isInView ? 'visible' : 'hidden'}>Content</div>
 */
export function useInView(options?: IntersectionObserverInit) {
  const ref = useRef<HTMLDivElement>(null);
  const [isInView, setIsInView] = useState(false);

  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => {
      if (entry?.isIntersecting) {
        setIsInView(true);
        observer.disconnect();
      }
    }, { threshold: 0.1, ...options });

    if (ref.current) {
      observer.observe(ref.current);
    }
    
    return () => observer.disconnect();
  }, [options]);

  return { ref, isInView };
}
