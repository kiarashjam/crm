import type { HTMLAttributes } from 'react';
import { motion } from 'motion/react';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const EASE = [0.16, 1, 0.3, 1] as const;

/**
 * A card or row that lifts and casts a soft shadow under the pointer. The same
 * lift on every page, and none at all with reduced motion.
 */
export const liftOnHover =
  'transition-[translate,box-shadow,border-color] duration-200 ease-out hover:-translate-y-0.5 hover:shadow-lg motion-reduce:transition-none motion-reduce:hover:translate-y-0';

type Tag = 'div' | 'li' | 'tr' | 'article' | 'section';

type StaggerItemProps = Omit<
  HTMLAttributes<HTMLElement>,
  'onAnimationStart' | 'onAnimationEnd' | 'onAnimationIteration' | 'onDrag' | 'onDragStart' | 'onDragEnd' | 'onDragEnter' | 'onDragLeave' | 'onDragOver' | 'onDrop'
> & {
  as?: Tag;
  /** Position in the list. Delays are capped, so a long list does not keep its last rows waiting. */
  index?: number;
};

/**
 * One item of a list or grid that rises into place just after the ones before it.
 * Table rows only fade, since a row cannot move on its own. Plays once, when the
 * item mounts; with reduced motion it is simply there.
 */
export function StaggerItem({ as = 'div', index = 0, children, ...rest }: StaggerItemProps) {
  const reduce = useMotionPreference();
  const Tag = motion[as] as typeof motion.div;
  const from = as === 'tr' ? { opacity: 0 } : { opacity: 0, y: 10 };
  return (
    <Tag
      initial={reduce ? false : from}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: EASE, delay: reduce ? 0 : Math.min(index, 12) * 0.035 }}
      {...rest}
    >
      {children}
    </Tag>
  );
}
