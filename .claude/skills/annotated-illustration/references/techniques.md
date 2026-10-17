# Choosing and constructing the view

Pick the view from what the reader must see, then build the drawing to serve it.
Each entry below says what it is for, how to construct it, and how it fails.

## Contents

- [Plain elevation / plan](#plain-elevation--plan)
- [Cutaway](#cutaway)
- [Sectional](#sectional)
- [Exploded](#exploded)
- [Ghosted](#ghosted)
- [Sequence (steps)](#sequence-steps)
- [Detail inset](#detail-inset)
- [Comparison pair](#comparison-pair)
- [Annotated screenshot or UI mock](#annotated-screenshot-or-ui-mock)
- [Flow over a picture](#flow-over-a-picture)
- [How much to draw](#how-much-to-draw)

---

## Plain elevation / plan

A straight-on view — front, side, or from above. The default, and the right
answer more often than people expect.

Use it when every part worth naming is visible from one side: a dashboard, a
floor plan, a keyboard, a form, a leaf.

Construct it flat and orthographic. No perspective, no vanishing point. Real
proportions, because a reader who knows the subject will notice if they're off
and will stop trusting the figure.

Fails when the interesting parts are inside or behind. If you find yourself
wanting to label something you can't see, change the view.

## Cutaway

The outer shell is sliced open over part of its area so the interior shows,
while the rest of the shell stays intact.

Use it when the reader needs the interior *and* needs to keep their bearings —
an engine, a kettle, a building, a battery, a layered cake of infrastructure.

Construct it by choosing a cut boundary that follows the form: a torn-edge arc,
a quarter wedge, or a rectangular window. Give the cut edge a visible thickness
— two parallel lines a few px apart, or a hatched band — so the shell reads as
material rather than as a hole. Keep the uncut shell lighter in weight than the
interior parts; the interior is the point.

Fails when the cut boundary is a plain straight line with no thickness. It then
looks like the drawing is simply unfinished.

## Sectional

A clean plane slices all the way through, showing only what that plane
intersects. Stricter and more technical than a cutaway.

Use it for layered things where the layering is the lesson: soil horizons, a
wall assembly, a PCB stack, a pipeline's stages, a cell membrane.

Construct it with cut material hatched (45° parallel lines; different angle or
spacing per material), uncut material behind the plane drawn in hairline, and a
small section indicator on a companion plan view showing where the cut was
taken — a pair of arrows labelled `A—A`.

Fails when everything is hatched the same way; the layers then merge into one
grey field.

## Exploded

Parts separated along their assembly axes, hovering apart in the order they fit
together.

Use it for assembly, disassembly, part inventories, and "what's inside the box".

Construct it by choosing one or two axes and sliding every part along them,
keeping the *order* true to assembly. Draw a thin dashed centreline through
each group of parts that share an axis — this is what makes an exploded view
readable rather than a pile of floating objects. Gaps should be roughly equal;
unequal gaps imply a relationship that isn't there.

Fails without the centrelines, and fails when parts are exploded in a direction
they could never be removed from.

## Ghosted

The outer body is drawn in hairline or at low opacity so interior parts show
through it, as though the shell were glass.

Use it when the interior needs to be seen *in position* relative to the whole,
and a cut would destroy too much of the outline — a phone, a mouse, a shoe, a
car body over its chassis.

Construct it with the shell at 25–35% opacity (or hairline `#D4D4D4`) and the
interior at full weight. Resist ghosting more than one layer; two layers of
semi-transparency becomes mush.

Fails when shell and interior end up at similar weights, so neither reads as in
front.

## Sequence (steps)

Several small frames in a row or grid, each a moment in a process.

Use it for anything with a before and after: assembly instructions, a gesture, a
state machine the user walks through, a chemical process.

Construct it with **identical framing in every panel** — same scale, same
viewpoint, same position of anything that doesn't move. Change only what the
step changes, and let the accent highlight the part that moved. Number the
panels; put each panel's short imperative under it ("Lift the tab"). Three to
six panels; beyond six, split into two figures.

Fails when the camera moves between panels. The reader then has to re-locate
everything each time and the sequence stops reading as one process.

## Detail inset

A small circled or boxed region of the main drawing, repeated beside it at
larger scale.

Use it when one small area carries a lot of labels, or when the scale needed to
see a detail would make the whole subject enormous.

Construct it by marking the region on the main drawing with a thin accent
circle or rectangle, then drawing two thin lines from that marker out to the
corners of the enlarged frame — a projection cone, which is what tells the
reader the inset *is* that region. Label the inset with its magnification or
just "detail". The enlarged frame gets the same border treatment as the marker.

Fails without the projection lines; the inset then floats, unconnected.

## Comparison pair

Two versions of the same subject side by side — before/after, correct/incorrect,
ours/theirs.

Use it when the lesson is a difference rather than a structure.

Construct both at identical scale and framing, aligned on a shared horizontal
axis, with a thin vertical rule between them. Annotate only the differences;
labelling what is identical in both wastes the reader's attention on the parts
that don't matter. If one side is the "wrong" one, mark it with a muted neutral
treatment rather than red crosses and green ticks — the accent should still mean
"look here", not "this is bad".

Fails when the two sides are at different scales, which makes every difference
suspect.

## Annotated screenshot or UI mock

A real or redrawn interface with callouts on its controls.

Prefer **redrawing** the UI as a simplified wireframe over pasting a screenshot:
a screenshot carries a specific user's data, a specific window size and a
specific build, all of which go stale and none of which the reader needs. A
wireframe at 2px ink with grey placeholder blocks for content stays true far
longer and prints cleanly.

Construct it with real proportions and real control positions, but generic
content: grey bars for text runs, a simple glyph for avatars, `—` for values.
Keep any text that is part of the UI's meaning (menu names, button labels) and
drop the rest. If you must use a real screenshot, crop tightly, redact personal
data, and draw the annotation layer on top in the accent.

Fails when it mixes screenshot and wireframe in one figure — the styles fight.

## Flow over a picture

A pipeline or data path drawn as a route across a picture of the real
components, rather than as abstract boxes.

Use it when both the *path* and the *physical arrangement* matter: a request
crossing a system's real services, water through a real building, a document
through real desks.

Construct the components first as small ghosted illustrations in their real
relative positions, then draw the route over them as a single accent path with
arrow markers, numbered where it passes each stage. The route should be one
continuous stroke, not a chain of separate arrows — continuity is what makes it
read as one journey.

Fails when the route doubles back over itself repeatedly; rearrange the
components instead.

---

## How much to draw

Draw the least that still reads as the thing. A kettle needs a silhouette, a
spout, a handle and a lid line — not a photorealistic rendering, and not a bare
rectangle either. Two tests:

- **Silhouette test.** Fill the drawing solid black in your head. Is it still
  recognisable? If not, the outline is wrong, and no amount of interior detail
  will save it.
- **Label test.** Every stroke that isn't being labelled and isn't needed for
  recognition is a candidate for deletion. Scenery that survives this test is
  the scenery that was doing real work.

Detail should also be *uneven*: dense where the labels are, sparse elsewhere.
Even detail across the whole subject reads as a photograph and gives the reader
no hint about where to look. Uneven detail is itself a form of annotation.
