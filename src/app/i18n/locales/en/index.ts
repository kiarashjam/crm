// The English catalogue: the source every other language translates, key for key.
import { common } from './common';
import { publicPages } from './publicPages';
import { messages } from './messages';
import { signing } from './signing';
import { settings } from './settings';
import { nav } from './nav';
import { members } from './members';
import { portal } from './portal';

export const sections = { common, publicPages, messages, signing, settings, nav, members, portal };

export const en = { ...common, ...publicPages, ...messages, ...signing, ...settings, ...nav, ...members, ...portal };
