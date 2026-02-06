import { useState, useEffect, useRef, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Circle, Hexagon, Triangle, ChevronDown, Sparkles } from 'lucide-react';

export { ChevronDown }; // Re-export for use in Homepage

// ============================================
// ANIMATION HOOKS
// ============================================

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

    if (ref.current) observer.observe(ref.current);
    return () => observer.disconnect();
  }, [options]);

  return { ref, isInView };
}

export function useMousePosition() {
  const [position, setPosition] = useState({ x: 0, y: 0 });
  
  useEffect(() => {
    const handleMove = (e: MouseEvent) => {
      setPosition({ x: e.clientX, y: e.clientY });
    };
    window.addEventListener('mousemove', handleMove);
    return () => window.removeEventListener('mousemove', handleMove);
  }, []);
  
  return position;
}

export function useParallax(speed = 0.5) {
  const [offset, setOffset] = useState(0);
  
  useEffect(() => {
    const handleScroll = () => {
      setOffset(window.scrollY * speed);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, [speed]);
  
  return offset;
}

// ============================================
// ANIMATED COMPONENTS
// ============================================

interface AnimatedSectionProps {
  children: ReactNode;
  className?: string;
  animation?: 'fade-up' | 'fade-left' | 'fade-right' | 'scale' | 'fade' | 'rotate' | 'flip';
  delay?: number;
}

export function AnimatedSection({ 
  children, 
  className = '', 
  animation = 'fade-up',
  delay = 0 
}: AnimatedSectionProps) {
  const { ref, isInView } = useInView();
  
  const animationClasses: Record<string, string> = {
    'fade-up': 'translate-y-20 opacity-0',
    'fade-left': '-translate-x-20 opacity-0',
    'fade-right': 'translate-x-20 opacity-0',
    'scale': 'scale-75 opacity-0',
    'fade': 'opacity-0',
    'rotate': 'rotate-12 opacity-0 scale-90',
    'flip': 'rotateX-90 opacity-0',
  };

  return (
    <div
      ref={ref}
      className={`transition-all duration-1000 ease-out ${className} ${
        isInView ? 'translate-y-0 translate-x-0 scale-100 rotate-0 opacity-100' : animationClasses[animation]
      }`}
      style={{ transitionDelay: `${delay}ms`, perspective: '1000px' }}
    >
      {children}
    </div>
  );
}

// Magnetic hover effect
export function MagneticButton({ children, className = '' }: { children: ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ x: 0, y: 0 });

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!ref.current) return;
    const rect = ref.current.getBoundingClientRect();
    const x = (e.clientX - rect.left - rect.width / 2) * 0.3;
    const y = (e.clientY - rect.top - rect.height / 2) * 0.3;
    setPosition({ x, y });
  };

  const handleMouseLeave = () => setPosition({ x: 0, y: 0 });

  return (
    <div
      ref={ref}
      className={className}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      style={{
        transform: `translate(${position.x}px, ${position.y}px)`,
        transition: 'transform 0.2s ease-out',
      }}
    >
      {children}
    </div>
  );
}

// Floating with rotation
interface FloatingElementProps {
  children: ReactNode;
  className?: string;
  delay?: number;
  duration?: number;
  distance?: number;
  rotate?: boolean;
}

export function FloatingElement({ 
  children, 
  className = '',
  delay = 0,
  duration = 4,
  distance = 15,
  rotate = true,
}: FloatingElementProps) {
  const uniqueId = useRef(`float-${Math.random().toString(36).substr(2, 9)}`);
  
  return (
    <div className={className}>
      <style>{`
        @keyframes ${uniqueId.current} {
          0%, 100% { 
            transform: translateY(0) ${rotate ? 'rotate(0deg)' : ''}; 
          }
          25% { 
            transform: translateY(-${distance}px) ${rotate ? 'rotate(2deg)' : ''}; 
          }
          75% { 
            transform: translateY(${distance * 0.5}px) ${rotate ? 'rotate(-2deg)' : ''}; 
          }
        }
      `}</style>
      <div style={{
        animation: `${uniqueId.current} ${duration}s ease-in-out infinite`,
        animationDelay: `${delay}s`,
      }}>
        {children}
      </div>
    </div>
  );
}

// Morphing blob shape
export function MorphingBlob({ className = '', color = 'orange' }: { className?: string; color?: string }) {
  return (
    <div className={className}>
      <style>{`
        @keyframes morph {
          0%, 100% { border-radius: 60% 40% 30% 70% / 60% 30% 70% 40%; transform: rotate(0deg); }
          25% { border-radius: 30% 60% 70% 40% / 50% 60% 30% 60%; }
          50% { border-radius: 50% 60% 30% 60% / 30% 60% 70% 40%; transform: rotate(180deg); }
          75% { border-radius: 60% 40% 60% 30% / 70% 30% 50% 60%; }
        }
      `}</style>
      <div 
        className={`bg-gradient-to-br from-${color}-400 to-${color}-600 opacity-30 blur-3xl`}
        style={{ animation: 'morph 15s ease-in-out infinite', width: '100%', height: '100%' }}
      />
    </div>
  );
}

// Glitch text effect
export function GlitchText({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <span className={`inline-block ${className}`}>
      {children}
    </span>
  );
}

// Ripple button
export function RippleButton({ children, className = '', to }: { children: ReactNode; className?: string; to: string }) {
  const [ripples, setRipples] = useState<{ x: number; y: number; id: number }[]>([]);

  const handleClick = (e: React.MouseEvent<HTMLAnchorElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const id = Date.now();
    setRipples(prev => [...prev, { x, y, id }]);
    setTimeout(() => setRipples(prev => prev.filter(r => r.id !== id)), 1000);
  };

  return (
    <Link to={to} className={`relative overflow-hidden ${className}`} onClick={handleClick}>
      <style>{`
        @keyframes ripple {
          to { transform: scale(4); opacity: 0; }
        }
      `}</style>
      {ripples.map(ripple => (
        <span
          key={ripple.id}
          className="absolute bg-white/30 rounded-full pointer-events-none"
          style={{
            left: ripple.x - 10,
            top: ripple.y - 10,
            width: 20,
            height: 20,
            animation: 'ripple 1s ease-out',
          }}
        />
      ))}
      {children}
    </Link>
  );
}

// 3D Card tilt effect
export function TiltCard({ children, className = '' }: { children: ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [transform, setTransform] = useState('');

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!ref.current) return;
    const rect = ref.current.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;
    const rotateX = (y - 0.5) * -20;
    const rotateY = (x - 0.5) * 20;
    setTransform(`perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(1.02, 1.02, 1.02)`);
  };

  const handleMouseLeave = () => setTransform('');

  return (
    <div
      ref={ref}
      className={`transition-transform duration-300 ease-out ${className}`}
      style={{ transform }}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      {children}
    </div>
  );
}

// Bouncing dots
export function BouncingDots({ className = '' }: { className?: string }) {
  return (
    <span className={`inline-flex gap-1 ${className}`}>
      {[0, 1, 2].map(i => (
        <span
          key={i}
          className="w-2 h-2 rounded-full bg-current"
          style={{
            animation: 'bounce-dot 1.4s ease-in-out infinite',
            animationDelay: `${i * 0.16}s`,
          }}
        />
      ))}
      <style>{`
        @keyframes bounce-dot {
          0%, 80%, 100% { transform: scale(0); }
          40% { transform: scale(1); }
        }
      `}</style>
    </span>
  );
}

// Gradient text
export function GradientText({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <span 
      className={`text-transparent bg-clip-text bg-gradient-to-r from-orange-600 via-amber-500 to-orange-600 ${className}`}
    >
      {children}
    </span>
  );
}

// Shaking element
export function ShakeOnHover({ children, className = '' }: { children: ReactNode; className?: string }) {
  const [isShaking, setIsShaking] = useState(false);
  
  return (
    <div 
      className={className}
      onMouseEnter={() => setIsShaking(true)}
      onAnimationEnd={() => setIsShaking(false)}
      style={isShaking ? { animation: 'shake 0.5s ease-in-out' } : {}}
    >
      <style>{`
        @keyframes shake {
          0%, 100% { transform: translateX(0); }
          10%, 30%, 50%, 70%, 90% { transform: translateX(-4px) rotate(-1deg); }
          20%, 40%, 60%, 80% { transform: translateX(4px) rotate(1deg); }
        }
      `}</style>
      {children}
    </div>
  );
}

// Particle explosion on click
export function ParticleExplosion({ children, className = '' }: { children: ReactNode; className?: string }) {
  const [particles, setParticles] = useState<{ id: number; x: number; y: number; angle: number }[]>([]);

  const handleClick = (e: React.MouseEvent) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const newParticles = Array.from({ length: 12 }, (_, i) => ({
      id: Date.now() + i,
      x,
      y,
      angle: (i * 30) * (Math.PI / 180),
    }));
    setParticles(prev => [...prev, ...newParticles]);
    setTimeout(() => setParticles(prev => prev.filter(p => !newParticles.find(np => np.id === p.id))), 1000);
  };

  return (
    <div className={`relative ${className}`} onClick={handleClick}>
      <style>{`
        @keyframes particle-fly {
          to { transform: translate(var(--tx), var(--ty)) scale(0); opacity: 0; }
        }
      `}</style>
      {particles.map(p => (
        <span
          key={p.id}
          className="absolute w-2 h-2 bg-orange-500 rounded-full pointer-events-none"
          style={{
            left: p.x,
            top: p.y,
            '--tx': `${Math.cos(p.angle) * 60}px`,
            '--ty': `${Math.sin(p.angle) * 60}px`,
            animation: 'particle-fly 0.6s ease-out forwards',
          } as React.CSSProperties}
        />
      ))}
      {children}
    </div>
  );
}

// Rotating border
export function RotatingBorder({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`relative p-[3px] rounded-2xl overflow-hidden ${className}`}>
      <style>{`
        @keyframes rotate-border {
          to { transform: rotate(360deg); }
        }
      `}</style>
      <div 
        className="absolute inset-0 bg-gradient-to-r from-orange-500 via-amber-500 to-orange-500"
        style={{ animation: 'rotate-border 3s linear infinite' }}
      />
      <div className="relative bg-white rounded-2xl">{children}</div>
    </div>
  );
}

// Wave animation for text
export function WaveText({ text, className = '' }: { text: string; className?: string }) {
  return (
    <span className={className}>
      <style>{`
        @keyframes wave-char {
          0%, 100% { transform: translateY(0); }
          50% { transform: translateY(-8px); }
        }
      `}</style>
      {text.split('').map((char, i) => (
        <span
          key={i}
          className="inline-block"
          style={{
            animation: 'wave-char 1s ease-in-out infinite',
            animationDelay: `${i * 0.05}s`,
          }}
        >
          {char === ' ' ? '\u00A0' : char}
        </span>
      ))}
    </span>
  );
}

// Decorative animated shapes
export function AnimatedShapes() {
  const parallax = useParallax(0.1);
  
  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      {/* Morphing blobs */}
      <MorphingBlob className="absolute -top-40 -right-40 w-96 h-96" color="orange" />
      <MorphingBlob className="absolute top-1/2 -left-40 w-80 h-80" color="amber" />
      <MorphingBlob className="absolute -bottom-40 right-1/4 w-72 h-72" color="rose" />
      
      {/* Floating geometric shapes */}
      <FloatingElement className="absolute top-20 left-[15%]" delay={0} duration={6} distance={20}>
        <div className="w-4 h-4 rotate-45 bg-orange-400/30" style={{ transform: `translateY(${parallax}px)` }} />
      </FloatingElement>
      <FloatingElement className="absolute top-40 right-[20%]" delay={1} duration={5} distance={15}>
        <div className="w-3 h-3 rounded-full bg-amber-400/40" style={{ transform: `translateY(${parallax * 1.5}px)` }} />
      </FloatingElement>
      <FloatingElement className="absolute bottom-40 left-[25%]" delay={2} duration={7} distance={18}>
        <Triangle className="w-5 h-5 text-orange-300/30" style={{ transform: `translateY(${parallax * 0.8}px)` }} />
      </FloatingElement>
      <FloatingElement className="absolute top-60 left-[10%]" delay={0.5} duration={4} distance={12}>
        <Hexagon className="w-6 h-6 text-amber-300/20" />
      </FloatingElement>
      <FloatingElement className="absolute bottom-60 right-[10%]" delay={1.5} duration={5.5} distance={16}>
        <Circle className="w-4 h-4 text-orange-400/25" />
      </FloatingElement>
    </div>
  );
}

// FAQ Item component
interface FAQItemProps {
  question: string;
  answer: string;
  icon: typeof Sparkles;
  color: string;
  index: number;
}

export function FAQItemNew({ 
  question, 
  answer, 
  icon: Icon, 
  color, 
  index 
}: FAQItemProps) {
  const [isOpen, setIsOpen] = useState(false);
  const { ref, isInView } = useInView();
  
  return (
    <div
      ref={ref}
      className={`transition-all duration-700 ${isInView ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-10'}`}
      style={{ transitionDelay: `${index * 100}ms` }}
    >
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="w-full group"
      >
        <div className={`relative bg-white rounded-2xl shadow-sm hover:shadow-xl transition-all duration-300 overflow-hidden border border-slate-100 ${
          isOpen ? 'ring-2 ring-offset-2 ring-orange-500' : ''
        }`}>
          {/* Decorative gradient stripe */}
          <div className={`absolute left-0 top-0 bottom-0 w-1.5 bg-gradient-to-b ${color}`} />
          
          <div className="p-6 pl-8">
            {/* Question Header */}
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                {/* Icon */}
                <div className={`w-12 h-12 rounded-xl bg-gradient-to-br ${color} flex items-center justify-center shadow-lg flex-shrink-0 group-hover:scale-110 transition-transform duration-300`}>
                  <Icon className="w-6 h-6 text-white" />
                </div>
                
                {/* Question text */}
                <h3 className="text-left text-lg font-semibold text-slate-900 group-hover:text-orange-600 transition-colors">
                  {question}
                </h3>
              </div>
              <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 transition-all duration-300 ${
                isOpen ? 'bg-slate-900 rotate-180' : 'bg-slate-100 group-hover:bg-slate-200'
              }`}>
                <ChevronDown className={`w-4 h-4 transition-colors duration-300 ${
                  isOpen ? 'text-white' : 'text-slate-400'
                }`} />
              </div>
            </div>
            
            {/* Answer */}
            <div className={`overflow-hidden transition-all duration-500 ease-out ${
              isOpen ? 'max-h-48 opacity-100 mt-4' : 'max-h-0 opacity-0 mt-0'
            }`}>
              <p className="text-slate-600 leading-relaxed pr-12">
                {answer}
              </p>
            </div>
          </div>
        </div>
      </button>
    </div>
  );
}
