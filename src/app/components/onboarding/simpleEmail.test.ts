// The simple welcome email: its HTML is made from the words, and read back from it.
import { describe, expect, it } from 'vitest';
import { buildSimpleHtml, designOf, readSimple, simpleDefaults, withoutSimpleMark } from './simpleEmail';

describe('the simple welcome email', () => {
  it('is read back as it was written', () => {
    const email = { ...simpleDefaults('fr'), message: 'Bonjour {{firstName}} {{lastName}},\n\nÀ **bientôt** --> <b>?</b>', color: '#db2777' };
    expect(readSimple(buildSimpleHtml(email))).toEqual(email);
    expect(designOf(buildSimpleHtml(email))).toBe('simple');
  });

  it('escapes what was typed, keeps the fields, and makes paragraphs, line breaks and bold', () => {
    const html = buildSimpleHtml({ ...simpleDefaults('en'), heading: 'Hi <Camille>', message: 'One\nline\n\n**Two** {{firstName}}' });
    expect(html).toContain('>Hi &lt;Camille&gt;</h1>');
    expect(html).toContain('>One<br>line</p>');
    expect(html).toContain('><strong>Two</strong> {{firstName}}</p>');
    expect(html).not.toContain('<Camille>');
  });

  it('is HTML of your own once edited by hand, and keeps no note at its top for the HTML editor', () => {
    const html = buildSimpleHtml(simpleDefaults('en'));
    expect(readSimple(html.replace('Choose my password', 'Go'))).toBeNull();
    expect(designOf(html.replace('Choose my password', 'Go'))).toBe('html');
    expect(withoutSimpleMark(html).startsWith('<div')).toBe(true);
    expect(designOf('')).toBe('portal');
  });

  it('keeps to a colour it can use', () => {
    const html = buildSimpleHtml({ ...simpleDefaults('en'), color: 'red;background:url(x)' });
    expect(html).not.toContain('url(x)');
    expect(readSimple(html)?.color).toBe('#0f172a');
  });
});
