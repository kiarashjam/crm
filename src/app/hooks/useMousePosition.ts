import { useEffect, useState } from 'react';

interface MousePosition {
  x: number;
  y: number;
}

/**
 * Hook to track mouse position
 * 
 * @returns { x, y } - Current mouse coordinates
 * 
 * @example
 * const { x, y } = useMousePosition();
 * return <div style={{ transform: `translate(${x}px, ${y}px)` }} />
 */
export function useMousePosition(): MousePosition {
  const [position, setPosition] = useState<MousePosition>({ x: 0, y: 0 });
  
  useEffect(() => {
    const handleMove = (e: MouseEvent) => {
      setPosition({ x: e.clientX, y: e.clientY });
    };
    
    window.addEventListener('mousemove', handleMove);
    return () => window.removeEventListener('mousemove', handleMove);
  }, []);
  
  return position;
}
