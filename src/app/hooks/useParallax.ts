import { useEffect, useState } from 'react';

/**
 * Hook for parallax scrolling effect
 * 
 * @param speed - Parallax speed multiplier (default: 0.5)
 * @returns offset - Current scroll offset adjusted by speed
 * 
 * @example
 * const offset = useParallax(0.3);
 * return <div style={{ transform: `translateY(${offset}px)` }} />
 */
export function useParallax(speed = 0.5): number {
  const [offset, setOffset] = useState(0);
  
  useEffect(() => {
    const handleScroll = () => {
      setOffset(window.scrollY * speed);
    };
    
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, [speed]);
  
  return offset;
}
