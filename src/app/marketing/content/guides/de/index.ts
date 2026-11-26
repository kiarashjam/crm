// The de guides, by id: one chunk with the de marketing pages.

import type { GuideId } from '../../../routes';
import type { GuideContent } from '../types';
import runAClub from './guide-run-a-club';
import memberRetention from './guide-member-retention';
import qrBill from './guide-qr-bill';
import excelToCrm from './guide-excel-to-crm';
import salesFollowUp from './guide-sales-follow-up';
import fadpChecklist from './guide-fadp-checklist';
import membershipApplications from './guide-membership-applications';
import chooseACrm from './guide-choose-a-crm';

export const deGuides: Record<GuideId, GuideContent> = {
  'guide-run-a-club': runAClub,
  'guide-member-retention': memberRetention,
  'guide-qr-bill': qrBill,
  'guide-excel-to-crm': excelToCrm,
  'guide-sales-follow-up': salesFollowUp,
  'guide-fadp-checklist': fadpChecklist,
  'guide-membership-applications': membershipApplications,
  'guide-choose-a-crm': chooseACrm,
};
