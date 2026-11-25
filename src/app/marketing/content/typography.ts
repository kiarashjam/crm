// French typography: a narrow no-break space before ? ! ; : and inside « », so a line never
// starts with the punctuation. Applied once to the whole French content. Link targets
// ("page:sales", "/login?mode=register") have no space before their colon or question
// mark, so they are left as they are.

const BEFORE = / ([?!;:»])/g;
const AFTER = /« /g;
const NNBSP = ' ';

export function frenchSpacing(text: string): string {
  return text.replace(BEFORE, `${NNBSP}$1`).replace(AFTER, `«${NNBSP}`);
}

/** The same value with frenchSpacing applied to every string in it. */
export function withFrenchSpacing<T>(value: T): T {
  if (typeof value === 'string') return frenchSpacing(value) as T;
  if (Array.isArray(value)) return value.map((v) => withFrenchSpacing(v)) as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value)) out[k] = withFrenchSpacing(v);
    return out as T;
  }
  return value;
}
