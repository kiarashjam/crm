// Every guide's title, description and short name, by language: small, so the site's header,
// footer and head can name every guide without loading the guides themselves.

import type { PageMeta } from '../../../meta';
import type { GuideId } from '../../../routes';
import type { SiteLang } from '../../../site';
import { runAClubMeta } from './guide-run-a-club';
import { memberRetentionMeta } from './guide-member-retention';
import { qrBillMeta } from './guide-qr-bill';
import { excelToCrmMeta } from './guide-excel-to-crm';
import { salesFollowUpMeta } from './guide-sales-follow-up';
import { fadpChecklistMeta } from './guide-fadp-checklist';
import { membershipApplicationsMeta } from './guide-membership-applications';
import { chooseACrmMeta } from './guide-choose-a-crm';

const ALL: Record<GuideId, Record<SiteLang, PageMeta>> = {
  'guide-run-a-club': runAClubMeta,
  'guide-member-retention': memberRetentionMeta,
  'guide-qr-bill': qrBillMeta,
  'guide-excel-to-crm': excelToCrmMeta,
  'guide-sales-follow-up': salesFollowUpMeta,
  'guide-fadp-checklist': fadpChecklistMeta,
  'guide-membership-applications': membershipApplicationsMeta,
  'guide-choose-a-crm': chooseACrmMeta,
};

function forLang(lang: SiteLang): Record<GuideId, PageMeta> {
  return Object.fromEntries(Object.entries(ALL).map(([id, meta]) => [id, meta[lang]])) as Record<GuideId, PageMeta>;
}

export const GUIDE_META: Record<SiteLang, Record<GuideId, PageMeta>> = {
  en: forLang('en'),
  fr: forLang('fr'),
  de: forLang('de'),
};
