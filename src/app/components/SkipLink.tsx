/** Skip to main content link for keyboard/screen reader users. Visible on focus. */
export const MAIN_CONTENT_ID = 'main-content';

export default function SkipLink() {
  return (
    <a
      href={`#${MAIN_CONTENT_ID}`}
      className="fixed left-4 top-4 z-[100] -translate-y-16 rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-lg transition-transform duration-150 focus:translate-y-0 focus:outline-none focus:ring-2 focus:ring-orange-500 focus:ring-offset-2 focus:ring-offset-white"
    >
      Skip to main content
    </a>
  );
}
