import { useLayoutEffect, useRef } from 'react';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

/**
 * Fade + slide in when the block enters the viewport. Same easing/timing across the app.
 */
export default function ScrollReveal({ children, y = 36, delay = 0, className = '', style = {} }) {
  const ref = useRef(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        el,
        { autoAlpha: 0, y },
        {
          autoAlpha: 1,
          y: 0,
          duration: 0.75,
          delay,
          ease: 'power2.out',
          scrollTrigger: {
            trigger: el,
            start: 'top 89%',
            once: true,
          },
        }
      );
    }, el);

    return () => ctx.revert();
  }, [y, delay]);

  return (
    <div ref={ref} className={className} style={style}>
      {children}
    </div>
  );
}
