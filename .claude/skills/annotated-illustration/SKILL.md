---
name: annotated-illustration
description: Draw an explanatory figure — a picture of a thing, with labels attached to its parts by leader lines. Use this whenever someone wants a diagram, figure, schematic, cutaway, exploded view, labeled screenshot, annotated UI mock, "anatomy of X", onboarding graphic, process picture, or asks you to "explain this visually", "show me how it works", "draw/illustrate/sketch this". Trigger it even when the word "annotated" never appears, and prefer it over Mermaid or hand-rolled SVG whenever the figure is a picture of a thing rather than a graph of boxes — the hard part is placing labels so they never collide, cross, or cover the drawing, and that is exactly what this skill handles.
---

# Annotated illustration

An annotated illustration is a drawing of something real — a device, a screen, a
building, a document, a data flow, a body, a pipeline — with short labels
anchored to specific parts of it. The drawing carries the structure; the labels
carry the vocabulary. Textbook cutaways, IKEA steps, patent figures and the
"anatomy of a ___" posters all work this way.

It is a different job from a flowchart. A flowchart is a graph: boxes and
arrows, where position means little. An illustration has a *subject* with real
spatial relationships, and the annotation layer sits on top without damaging it.
If what you are drawing is genuinely a graph of nodes, use Mermaid or a layout
engine instead and don't force it through here.

## The shape of the work

Work in this order. Most bad figures come from drawing first and discovering
afterwards that there is nowhere to put the labels.

1. **Name the one thing the reader should understand.** A figure that teaches
   two lessons teaches neither. Write that sentence down — it becomes the
   caption, and it tells you which parts deserve a label and which are just
   scenery.
2. **Pick the view** (plan, elevation, cutaway, exploded, sequence, ghosted —
   see `references/techniques.md`). The view is chosen by what must be visible,
   not by what is easiest to draw.
3. **Budget the canvas before drawing.** Reserve the label gutters first, then
   fit the subject into what is left. See *Canvas budget* below.
4. **Draw the subject** in neutral ink, with no labels at all. Check that it
   reads as the thing it is with the annotations switched off.
5. **Attach the annotation layer** — anchor dots, leader lines, labels — in the
   accent colour, as a separate group. Use `scripts/layout_callouts.py` so the
   leaders cannot cross.
6. **Lint it** with `scripts/check_illustration.py`, then render it with
   `scripts/render.cjs` and look at the PNG yourself. The linter catches
   geometry; only your eye catches "this doesn't look like a kettle".

## Canvas budget

Decide the frame up front and keep the subject out of the gutters.

| Orientation | viewBox | Side gutters | Top band | Bottom band |
|---|---|---|---|---|
| Landscape (default) | `0 0 1200 800` | 240 each | 72 (title) | 64 (caption) |
| Portrait | `0 0 900 1200` | 200 each | 72 | 64 |
| Square / dense | `0 0 1000 1000` | 220 each | 72 | 64 |

So a landscape subject lives inside roughly x ∈ [260, 940], y ∈ [88, 720]. That
feels wasteful until the labels arrive; then it is exactly enough. If the
subject genuinely needs the full width, move to numbered callouts with a key
block (see below) instead of stealing the gutters.

Labels go **in the gutters**, not on the drawing. A label sitting on top of the
subject hides the thing it is naming, and it is the single most common way these
figures fail.

## Drawing the subject

- **Ink, not colour.** Outline `#1A1A1A` at 2px, interior detail `#6B6B6B` at
  1.25px, hairlines and construction `#D4D4D4` at 1px. Paper `#FDFCFA`, or no
  background at all if it will sit on a page.
- **Exactly one accent**, used *only* by the annotation layer — a warm
  `#C2410C` or a cool `#2563EB` both work. When the accent belongs solely to the
  annotations, the reader's eye separates "the thing" from "what we're saying
  about the thing" with no effort at all. Spend the accent on the subject and
  that separation is gone.
- **Fills**: prefer none, or a 6–10% tint of the ink. Where you need to
  distinguish materials or regions, use the hatch patterns in
  `references/svg-recipes.md` rather than more colours.
- **No drop shadows, no gradients, no bevels, no emoji.** They read as clip art
  and they survive neither printing nor dark mode.
- **Line weight carries depth.** Nearer edges heavier, far edges lighter. This
  does more for legibility than any amount of shading.
- **Draw it as groups**: `<g id="subject">`, `<g id="annotations">`,
  `<g id="frame">`. Someone will want to restyle or re-label it later, and
  grouped output makes that a two-minute job.

## The annotation layer

Each callout is three parts: an **anchor dot** on the part being named, a
**leader line** out to the gutter, and the **label** itself.

- Anchor: filled circle, r=3, accent colour, placed *on* the feature — not
  floating near it.
- Leader: 1px accent, one elbow at most. The final segment runs horizontally
  into the label for at least 24px so the text sits on the line's axis. Curves,
  dogs-legs and multi-bend routes make the reader work.
- Label: 14px, weight 500, ink `#1A1A1A` (not the accent — accent on text at
  small sizes hurts contrast). Right-aligned in the left gutter, left-aligned in
  the right gutter, vertically centred on its leader's horizontal segment.
- Keep labels to 1–4 words, wrapping at ~24 characters. If a label needs a
  sentence, it is a caption or a key entry, not a label.
- **Give every callout its own anchor.** Two dots closer than ~18px read as
  one, and their leaders fan out from a single spot, so the reader can't tell
  which label means which. Anchor each on a distinct part of the feature — the
  hub and the path leading to it, not two points on the same edge.
- **Leaders must not cross.** Crossing leaders make the reader trace lines with
  a finger. `scripts/layout_callouts.py` guarantees this by assigning each
  anchor to the nearer gutter and then keeping label order monotonic in the
  anchors' vertical order.
- **Never shrink type to make a label fit.** Move the label, shorten the text,
  or switch to numbered callouts. A figure with five type sizes looks accidental.

### Numbered callouts

When there are more than about eight labels, or the labels are long, or the
subject must use the full width, switch to numbers: a small accent disc with a
white numeral at each anchor, and a key block listing `1 — Water inlet` below or
beside the figure. The figure stays clean and the key can carry a full phrase
per item. Keep the numbering reading order sane — clockwise from top-left, or
in the order the reader will actually encounter the parts.

## Scripts

The two Python scripts are plain Python 3, no dependencies; the renderer is a
small Node script. Run them from the skill directory or with an absolute path.

**`scripts/layout_callouts.py`** — turns a list of anchors + label texts into a
ready-to-paste SVG `<g>` of dots, leaders and labels, with gutter assignment,
collision-free vertical packing and non-crossing leaders already solved.

```bash
python3 scripts/layout_callouts.py callouts.json > annotations.svg
python3 scripts/layout_callouts.py callouts.json --numbered > annotations.svg
```

```json
{
  "width": 1200, "height": 800,
  "gutter": 240, "top": 88, "bottom": 736,
  "accent": "#C2410C",
  "callouts": [
    {"x": 520, "y": 210, "text": "Thermostat probe"},
    {"x": 700, "y": 330, "text": "Heating element"},
    {"x": 455, "y": 540, "text": "Water level window", "side": "left"}
  ]
}
```

`side` is optional — omit it and the script picks the nearer gutter. Do the
arithmetic here rather than by hand; eyeballing label y-positions is where the
hours go.

**`scripts/check_illustration.py`** — lints a finished SVG and exits non-zero on
problems: text outside the viewBox, overlapping label boxes, leaders that cross
or lie on top of each other, anchors too close to tell apart, more than three
font sizes, missing `<title>`/`<desc>`, accent colour leaking into the subject
group.

```bash
python3 scripts/check_illustration.py figure.svg
```

Run it before showing the figure. It is fast and it catches the things that are
embarrassing to miss.

**`scripts/render.cjs`** — renders the SVG to a PNG at its own size, using
Playwright's Chromium (a local or global install), so you can look at it.
`--dark` renders with the dark palette; check both whenever the figure will be
seen in an app that follows the system theme.

```bash
node scripts/render.cjs figure.svg            # figure.png beside it
node scripts/render.cjs figure.svg out.png --dark
```

## Delivering the figure

- Default output is **one self-contained `.svg`** — no external fonts, no
  external images. Set `font-family="Inter, 'Helvetica Neue', Arial, sans-serif"`
  on the root so it degrades gracefully, and keep text as `<text>` elements so
  it stays searchable, translatable and screen-reader friendly.
- Always include `<title>` and `<desc>` as the first children of the root
  `<svg>`. The title is the figure's name; the desc is the sentence from step 1.
- Add `role="img"` and `aria-labelledby` pointing at them.
- If the person will view it in chat, publish it as an artifact (an HTML page
  with the SVG inline) so they can actually see it; if they asked for a file,
  hand them the `.svg`. If they need a raster, note that any browser or
  `rsvg-convert` will produce a PNG from it.
- Dark mode: because the subject is ink-on-paper, a figure meant for a dark
  background needs its ink and paper swapped. Define the palette as CSS custom
  properties on the root `<svg>` and override them in a
  `@media (prefers-color-scheme: dark)` block inside a `<style>` element — the
  recipe is in `references/svg-recipes.md`.

## A worked example

`assets/example-kettle.svg` is a finished figure built exactly this way — a
cutaway kettle with eleven callouts, clean against the linter. Open it when you
want to see how the pieces fit together: the grouped structure, the CSS
custom-property palette, the torn cut edge with hatched thickness, the gutter
labels, the title and caption bands.

`assets/example-kettle.callouts.json` is the spec that produced its annotation
layer. Copying it and swapping the anchors is the fastest way to start.

## Reference files

- `references/techniques.md` — the view types (cutaway, exploded, ghosted,
  sectional, sequence, detail-inset, comparison pair), what each is good for,
  and how to construct it. Read it at step 2, when choosing the view.
- `references/svg-recipes.md` — copy-paste SVG for leader elbows, number badges,
  dimension lines, brackets, region highlights, hatch patterns, arrow markers,
  key blocks and the dark-mode palette. Read it while drawing.
