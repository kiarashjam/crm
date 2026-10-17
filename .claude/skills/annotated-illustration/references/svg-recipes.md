# SVG recipes

Copy-paste fragments for the parts that are fiddly to derive each time. All
assume the palette below and a `viewBox` in the sizes given in SKILL.md.

## Contents

- [Document skeleton](#document-skeleton)
- [Palette and dark mode](#palette-and-dark-mode)
- [Leader line with one elbow](#leader-line-with-one-elbow)
- [Anchor dot and number badge](#anchor-dot-and-number-badge)
- [Key block](#key-block)
- [Arrow markers](#arrow-markers)
- [Dimension line](#dimension-line)
- [Bracket (spanning several parts)](#bracket-spanning-several-parts)
- [Region highlight](#region-highlight)
- [Hatch patterns](#hatch-patterns)
- [Detail inset projection](#detail-inset-projection)
- [Cut edge with thickness](#cut-edge-with-thickness)
- [Exploded-view centreline](#exploded-view-centreline)
- [Title and caption](#title-and-caption)
- [Wrapping label text](#wrapping-label-text)

---

## Document skeleton

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 800"
     width="1200" height="800" role="img" aria-labelledby="figTitle figDesc"
     font-family="Inter, 'Helvetica Neue', Arial, sans-serif">
  <title id="figTitle">Anatomy of an electric kettle</title>
  <desc id="figDesc">Cutaway of a kettle showing how the thermostat cuts power
    when steam reaches the bimetallic strip in the handle.</desc>
  <style>
    :root, svg { --ink:#1A1A1A; --mid:#6B6B6B; --hair:#D4D4D4;
                 --paper:#FDFCFA; --accent:#C2410C; }
    @media (prefers-color-scheme: dark) {
      svg { --ink:#F2F0ED; --mid:#A3A3A3; --hair:#3F3F3F;
            --paper:#141414; --accent:#FB923C; }
    }
    .ink    { stroke:var(--ink);  stroke-width:2;    fill:none;
              stroke-linejoin:round; stroke-linecap:round; }
    .detail { stroke:var(--mid);  stroke-width:1.25; fill:none; }
    .hair   { stroke:var(--hair); stroke-width:1;    fill:none; }
    .leader { stroke:var(--accent); stroke-width:1;  fill:none; }
    .label  { fill:var(--ink); font-size:14px; font-weight:500; }
    .caption{ fill:var(--mid); font-size:13px; font-style:italic; }
    .figtitle { fill:var(--ink); font-size:20px; font-weight:600; }
  </style>

  <rect width="100%" height="100%" fill="var(--paper)"/>
  <g id="subject">     <!-- the drawing, ink only --></g>
  <g id="annotations"> <!-- dots, leaders, labels, accent only --></g>
  <g id="frame">       <!-- title, caption, key --></g>
</svg>
```

Keeping the palette in CSS custom properties is what makes dark mode a
four-line override rather than a find-and-replace across every element.

## Palette and dark mode

| Token | Light | Dark | Used for |
|---|---|---|---|
| `--ink` | `#1A1A1A` | `#F2F0ED` | Subject outline, label text |
| `--mid` | `#6B6B6B` | `#A3A3A3` | Interior detail, captions |
| `--hair` | `#D4D4D4` | `#3F3F3F` | Construction lines, ghosting |
| `--paper` | `#FDFCFA` | `#141414` | Background |
| `--accent` | `#C2410C` | `#FB923C` | Annotation layer **only** |

Cool alternative accent: `#2563EB` light / `#60A5FA` dark. Pick one accent and
keep it; a second accent colour immediately reads as a second meaning, and the
reader will spend effort looking for a distinction that isn't there.

## Leader line with one elbow

The last segment must be horizontal so the label sits on its axis. `ax,ay` is
the anchor on the drawing; `lx,ly` is where the label's text baseline sits.
Bend at `lx ∓ run` where `run ≥ 24`.

```svg
<!-- anchor to the LEFT gutter (label text right-aligned at lx) -->
<path class="leader" d="M 520 210 L 300 148 L 276 148"/>
<text class="label" x="268" y="153" text-anchor="end">Thermostat probe</text>

<!-- anchor to the RIGHT gutter (label text left-aligned at lx) -->
<path class="leader" d="M 700 330 L 940 300 L 964 300"/>
<text class="label" x="972" y="305" text-anchor="start">Heating element</text>
```

The `y` of the `<text>` is the baseline, which sits half a cap-height below the
line's vertical centre — ~5px for 14px type, which is the `+5` you see above.
Get this wrong and every label floats slightly above or below its own leader,
which looks like carelessness even when nobody can say why.

`scripts/layout_callouts.py` emits exactly this, with the elbow positions and
the non-crossing ordering already solved. Prefer it over placing these by hand.

## Anchor dot and number badge

```svg
<!-- plain anchor -->
<circle cx="520" cy="210" r="3" fill="var(--accent)"/>

<!-- numbered callout: disc + numeral, for dense figures -->
<g>
  <circle cx="520" cy="210" r="11" fill="var(--accent)"/>
  <text x="520" y="214.5" text-anchor="middle" font-size="12"
        font-weight="600" fill="var(--paper)">3</text>
</g>
```

A numeral needs r=11 for one digit, r=12 for two. Below that the digit crowds
the disc edge and stops reading at small display sizes.

## Key block

Pairs with numbered callouts. Two columns if there are more than six entries.

```svg
<g id="key" transform="translate(260, 756)">
  <text class="label" x="0" y="0" font-weight="600">Key</text>
  <g transform="translate(0,22)">
    <circle cx="8" cy="-4" r="8" fill="var(--accent)"/>
    <text x="8" y="0" text-anchor="middle" font-size="10"
          font-weight="600" fill="var(--paper)">1</text>
    <text class="label" x="26" y="0">Cold water inlet</text>
  </g>
  <!-- repeat at translate(0,44), (0,66), … -->
</g>
```

## Arrow markers

Define once, reuse. Markers inherit the path's stroke via `context-stroke`
where supported; the explicit fill is the safe fallback.

```svg
<defs>
  <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5"
          markerWidth="6" markerHeight="6" orient="auto-start-reverse">
    <path d="M 0 0 L 10 5 L 0 10 z" fill="var(--accent)"/>
  </marker>
  <marker id="tick" viewBox="0 0 10 10" refX="5" refY="5"
          markerWidth="8" markerHeight="8" orient="auto">
    <path d="M 5 0 L 5 10" stroke="var(--mid)" stroke-width="1.5"/>
  </marker>
</defs>

<path class="leader" marker-end="url(#arrow)" d="M 400 500 C 480 560, 560 560, 640 500"/>
```

Use an arrowhead only when direction is part of the meaning (motion, flow,
sequence). A leader line pointing at a static part should end in a dot, not an
arrow — an arrowhead there implies movement that isn't happening.

## Dimension line

```svg
<g class="hair">
  <path d="M 320 640 L 320 672"/>              <!-- extension -->
  <path d="M 880 640 L 880 672"/>
  <path d="M 320 660 L 880 660" marker-start="url(#tick)" marker-end="url(#tick)"/>
</g>
<text class="caption" x="600" y="656" text-anchor="middle">1.8 L</text>
```

## Bracket (spanning several parts)

For one label that names a group rather than a single part.

```svg
<path class="leader"
      d="M 944 220 q 10 0 10 10 L 954 300 q 0 10 10 10 q -10 0 -10 10 L 954 390 q 0 10 -10 10"/>
<text class="label" x="978" y="314" text-anchor="start">Control assembly</text>
```

The small mid-bracket nub at the vertical centre is where the leader leaves —
it signals "all of this", which a plain straight line does not.

## Region highlight

```svg
<rect x="470" y="180" width="140" height="90" rx="6"
      fill="var(--accent)" fill-opacity="0.08"
      stroke="var(--accent)" stroke-width="1" stroke-dasharray="4 3"/>
```

Keep the fill opacity at or below 0.10. Above that it starts to obscure the
drawing underneath, which defeats the purpose of highlighting it.

## Hatch patterns

Different angle *and* spacing per material, so they stay distinct in greyscale
and at small sizes.

```svg
<defs>
  <pattern id="hatchA" width="6" height="6" patternTransform="rotate(45)"
           patternUnits="userSpaceOnUse">
    <line x1="0" y1="0" x2="0" y2="6" stroke="var(--mid)" stroke-width="1"/>
  </pattern>
  <pattern id="hatchB" width="10" height="10" patternTransform="rotate(-45)"
           patternUnits="userSpaceOnUse">
    <line x1="0" y1="0" x2="0" y2="10" stroke="var(--mid)" stroke-width="1"/>
  </pattern>
  <pattern id="dots" width="8" height="8" patternUnits="userSpaceOnUse">
    <circle cx="2" cy="2" r="1" fill="var(--mid)"/>
  </pattern>
</defs>

<path d="…" fill="url(#hatchA)" stroke="var(--ink)" stroke-width="2"/>
```

## Detail inset projection

```svg
<!-- marker on the main drawing -->
<circle cx="640" cy="300" r="42" class="leader" stroke-dasharray="4 3"/>
<!-- projection cone to the enlarged frame -->
<path class="hair" d="M 676 272 L 940 150  M 676 328 L 940 470"/>
<!-- the enlarged frame -->
<circle cx="1070" cy="310" r="160" class="leader" stroke-dasharray="4 3" fill="var(--paper)"/>
<text class="caption" x="1070" y="492" text-anchor="middle">Detail · 4×</text>
```

## Cut edge with thickness

What makes a cutaway read as sliced material rather than as a missing piece.

```svg
<path class="ink" d="M 420 180 C 470 240, 470 420, 420 480"/>  <!-- outer face -->
<path class="detail" d="M 432 186 C 480 243, 480 417, 432 474"/> <!-- inner face -->
<path d="M 420 180 C 470 240, 470 420, 420 480 L 432 474
         C 480 417, 480 243, 432 186 Z" fill="url(#hatchA)"/>    <!-- the material -->
```

## Exploded-view centreline

```svg
<path class="hair" stroke-dasharray="12 4 2 4" d="M 600 120 L 600 700"/>
```

The long-short-short dash is the conventional centreline and reads instantly as
"these parts share an axis". A plain dashed line reads as "hidden edge" instead
and will be misunderstood by anyone used to technical drawings.

## Title and caption

```svg
<text class="figtitle" x="260" y="48">Anatomy of an electric kettle</text>
<text class="caption"  x="260" y="784">
  The bimetallic strip bends as steam reaches it, opening the contacts and
  cutting power — the kettle switches itself off.
</text>
```

The caption carries the sentence from step 1 of the workflow. A figure whose
caption is just a repeat of its title is wasting a line that could teach.

## Wrapping label text

SVG `<text>` does not wrap. Break it yourself with `<tspan>` and a fixed line
height; keep the same `text-anchor` on every line.

```svg
<text class="label" x="268" y="146" text-anchor="end">
  <tspan x="268" dy="0">Bimetallic</tspan>
  <tspan x="268" dy="17">strip</tspan>
</text>
```

Wrap at ~24 characters, and vertically centre the block on the leader by
shifting the first baseline up by `(lines - 1) × 17 / 2`. `layout_callouts.py`
does this arithmetic for you.
