// What differs between importing leads, contacts and companies: the words, where the
// records go, and the template a person can start from. Everything else is one page
// (ImportPage), because the server imports all three the same way.

import type { ElementType } from 'react';
import { Building2, UserCircle, Users } from 'lucide-react';
import type { HeroTone } from '@/app/components/PageHero';
import type { ImportKind } from '@/app/api/imports';
import { currentNumberLocale } from '@/app/i18n/current';

export interface ImportKindInfo {
  kind: ImportKind;
  one: string;
  many: string;
  title: string;
  subtitle: string;
  icon: ElementType;
  tone: HeroTone;
  /** The list the imported records are on. */
  listPath: string;
  listName: string;
  /** People are matched by email, or name and phone; companies by name or domain. */
  matchedBy: string;
  /** What a match is not, when that needs saying. */
  matchNote?: string;
  sampleFileName: string;
  sample: string;
}

// A couple share a landline and colleagues a switchboard, so a phone number alone does
// not make two people one; the server adds the row and notes who else has the number.
const SHARED_PHONE = 'Someone who only shares a phone number with a person here is added, with a note to check they are not the same person.';

export const IMPORT_KINDS: Record<ImportKind, ImportKindInfo> = {
  lead: {
    kind: 'lead',
    one: 'lead',
    many: 'leads',
    title: 'Import leads',
    subtitle: 'Bring in a spreadsheet of leads, check what will happen, and undo it if it was the wrong file',
    icon: Users,
    tone: 'orange',
    listPath: '/leads',
    listName: 'Leads',
    matchedBy: 'the same email, or the same name and phone number',
    matchNote: SHARED_PHONE,
    sampleFileName: 'leads_template.csv',
    sample: [
      'name,email,phone,company,job title,source,status,notes',
      'Anna Müller,anna.mueller@example.com,+41 79 123 45 67,Müller Treuhand AG,Partner,Referral,New,Met at the spring open evening',
      'Luca Bernasconi,luca.bernasconi@example.com,079 765 43 21,,,Website,Contacted,"Asked about family membership.',
      'Call after 5pm"',
    ].join('\r\n'),
  },
  contact: {
    kind: 'contact',
    one: 'contact',
    many: 'contacts',
    title: 'Import contacts',
    subtitle: 'Bring in a spreadsheet of people, check what will happen, and undo it if it was the wrong file',
    icon: UserCircle,
    tone: 'emerald',
    listPath: '/contacts',
    listName: 'Contacts',
    matchedBy: 'the same email, or the same name and phone number',
    matchNote: SHARED_PHONE,
    sampleFileName: 'contacts_template.csv',
    sample: [
      'first name,last name,email,phone,company,job title,notes',
      'Anna,Müller,anna.mueller@example.com,+41 79 123 45 67,Müller Treuhand AG,Partner,',
      'Marc,Dubois,marc.dubois@example.com,022 345 67 89,Dubois Vins SA,Owner,Prefers French',
    ].join('\r\n'),
  },
  company: {
    kind: 'company',
    one: 'company',
    many: 'companies',
    title: 'Import companies',
    subtitle: 'Bring in a spreadsheet of companies, check what will happen, and undo it if it was the wrong file',
    icon: Building2,
    tone: 'blue',
    listPath: '/companies',
    listName: 'Companies',
    matchedBy: 'the same name, or the same domain',
    sampleFileName: 'companies_template.csv',
    sample: [
      'name,domain,website,industry,size,location,notes',
      'Müller Treuhand AG,mueller-treuhand.example,https://www.mueller-treuhand.example,Accounting,11-50,Zürich,',
      'Dubois Vins SA,dubois-vins.example,,Wine,1-10,Genève,Supplies the club cellar',
    ].join('\r\n'),
  },
};

/** "1 lead", "3 leads". */
export function count(n: number, info: Pick<ImportKindInfo, 'one' | 'many'>): string {
  return `${n.toLocaleString(currentNumberLocale())} ${n === 1 ? info.one : info.many}`;
}
