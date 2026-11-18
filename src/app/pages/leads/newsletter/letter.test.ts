import { describe, expect, it } from 'vitest';
import { buildLetterHtml, imagesFromHtml, inLanguage, isLetterDefault, letterDefaults, siteOf } from './letter';

const WELCOME = [
  '<img src="https://api.cadencee.ai/api/public/email-images/hero.jpg" width="600" height="500" alt="">',
  '<p>Bonjour {{firstName}},</p>',
  '<img src="https://api.cadencee.ai/api/public/email-images/pool.jpg" width="297" height="230">',
  '<img src="https://api.cadencee.ai/api/public/email-images/salon.jpg" width="213" height="230">',
  '<img src="https://api.cadencee.ai/api/public/email-images/logo.png" width="224" height="42">',
].join('\n');

describe("the invitation, in the Founders' Letter", () => {
  it('takes the welcome letter’s cover, two photos and logo', () => {
    expect(imagesFromHtml(WELCOME)).toEqual({
      cover: 'https://api.cadencee.ai/api/public/email-images/hero.jpg',
      photos: [
        'https://api.cadencee.ai/api/public/email-images/pool.jpg',
        'https://api.cadencee.ai/api/public/email-images/salon.jpg',
      ],
      logo: 'https://api.cadencee.ai/api/public/email-images/logo.png',
    });
    expect(imagesFromHtml('<p>No images</p>')).toEqual({ cover: null, photos: [], logo: null });
    expect(imagesFromHtml(null)).toEqual({ cover: null, photos: [], logo: null });
    // One embedded in the HTML reaches nobody: it is not taken.
    expect(imagesFromHtml('<img src="data:image/png;base64,AAAA">').cover).toBeNull();
  });

  it('carries the letter’s parts, with the lead’s link and code for the server to fill in', () => {
    const html = buildLetterHtml(letterDefaults('fr', imagesFromHtml(WELCOME)), 'fr', 'pavillon46.ch');
    expect(html).toContain('href="{{bookingUrl}}"');
    expect(html).toContain('{{bookingCode}}');
    expect(html).toContain('Rendez-vous privé');
    expect(html).toContain('Venez découvrir<br><em style="font-style:italic;color:#E8846A;">{{workspace}}.</em>');
    expect(html).toContain('Bonjour {{firstName|à vous}},');
    expect(html).toContain('Trois gestes,');
    expect(html).toContain('Choisissez un jour');
    expect(html).toContain('Votre code');
    expect(html).toContain('src="https://api.cadencee.ai/api/public/email-images/hero.jpg"');
    expect(html).toContain('src="https://api.cadencee.ai/api/public/email-images/logo.png"');
    expect(html).toContain('href="https://pavillon46.ch"');
    expect(html).toContain('#9B8EB9');
    expect(html).toContain('Cormorant Garamond');
  });

  it('leaves out what is empty, and the steps when they are off', () => {
    const plain = buildLetterHtml(
      { ...letterDefaults('en'), quote: '', duration: '', place: '', steps: false, images: { cover: null, photos: [], logo: null } },
      'en',
    );
    expect(plain).not.toContain('How it works');
    expect(plain).not.toContain('<img');
    expect(plain).not.toContain('Duration');
    expect(plain).not.toContain('#9B8EB9;"><br>');
    // Without a logo, the footer names the workspace.
    expect(plain).toMatch(/text-transform:uppercase;[^"]*">\{\{workspace\}\}<\/div>/);
    expect(plain).toContain('Your code');
  });

  it('writes what is typed as text, never as HTML, and takes only web images', () => {
    const html = buildLetterHtml(
      { ...letterDefaults('en'), title: '<script>alert(1)</script>', message: 'A **bold** idea\n\nSecond', images: { cover: 'javascript:alert(1)', photos: [], logo: null } },
      'en',
    );
    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).toContain('A <strong>bold</strong> idea');
    expect(html).not.toContain('javascript:');
  });

  it('changes language without losing its images or steps', () => {
    const images = imagesFromHtml(WELCOME);
    const fr = { ...letterDefaults('fr', images), steps: false };
    expect(isLetterDefault(fr, 'fr')).toBe(true);
    const en = inLanguage(fr, 'en');
    expect(en.button).toBe('Book my meeting');
    expect(en.images).toEqual(images);
    expect(en.steps).toBe(false);
    expect(isLetterDefault({ ...fr, message: 'Changed' }, 'fr')).toBe(false);
  });

  it('names the site of the booking page', () => {
    expect(siteOf('https://www.pavillon46.ch/rendez-vous')).toBe('pavillon46.ch');
    expect(siteOf('not a url')).toBeNull();
    expect(siteOf(null)).toBeNull();
  });
});
