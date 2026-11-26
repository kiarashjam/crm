import type { PageMeta } from '../../../meta';
import type { SiteLang } from '../../../site';

/** A guide's title, description and short name in each language (titles ≤ 60, descriptions ≤ 155). */
export type GuideMeta = Record<SiteLang, PageMeta>;
