#!/usr/bin/env python3
"""Lay out callout labels around an illustration and emit an SVG group.

Reads a JSON spec describing the canvas and a list of anchor points with label
text, and writes an SVG <g id="annotations"> containing anchor dots, leader
lines and labels -- or, with --numbered, numbered discs plus a key block.

The two things this solves that are tedious by hand:

  * Leaders never cross. Each anchor goes to the nearer gutter, and within a
    gutter the labels keep the same top-to-bottom order as their anchors, which
    is sufficient to guarantee no crossings.
  * Labels never overlap. Vertical positions are the least-squares closest
    arrangement to each label's ideal height that still respects the minimum
    gap, found with pool-adjacent-violators rather than by nudging by hand.

Usage:
    python3 layout_callouts.py spec.json  > annotations.svg
    python3 layout_callouts.py spec.json --numbered > annotations.svg

Spec:
    {
      "width": 1200, "height": 800,
      "gutter": 240,          # width of each side gutter
      "top": 88,              # top of the band labels may occupy
      "bottom": 736,          # bottom of that band
      "accent": "#C2410C",
      "font_size": 14,
      "callouts": [
        {"x": 520, "y": 210, "text": "Thermostat probe"},
        {"x": 700, "y": 330, "text": "Heating element", "side": "right"}
      ]
    }

Only "callouts" is required; everything else falls back to the landscape
defaults in SKILL.md.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, field

# Width of an average glyph as a fraction of font size, for Inter / Helvetica
# at weight 500. Used only to decide where to wrap, so an estimate is fine.
CHAR_W_RATIO = 0.55
LINE_H = 17          # baseline-to-baseline for 14px labels
GAP = 12             # minimum clear space between two label blocks
RUN = 28             # horizontal segment leading into the label
TEXT_PAD = 8         # gap between the end of the leader and the text
EDGE_PAD = 20        # keep labels this far from the canvas edge
BASELINE_SHIFT = 5   # baseline sits this far below the line's vertical centre


@dataclass
class Label:
    index: int
    ax: float
    ay: float
    lines: list[str]
    side: str
    y: float = 0.0
    height: float = field(default=0.0)


def wrap(text: str, max_chars: int) -> list[str]:
    """Greedy wrap on spaces. Words longer than the limit get their own line."""
    words, lines, cur = text.split(), [], ""
    for w in words:
        trial = f"{cur} {w}".strip()
        if len(trial) <= max_chars or not cur:
            cur = trial
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines or [""]


def pava(targets: list[float], lower: float, upper: float) -> list[float]:
    """Isotonic regression: nearest non-decreasing sequence to `targets`.

    Clamping the targets into [lower, upper] first is enough to keep the result
    inside those bounds too, because every output value is a mean of inputs.
    """
    vals = [min(max(t, lower), upper) for t in targets]
    # Each block holds (sum, count); merge while the previous block is higher.
    blocks: list[list[float]] = []
    for v in vals:
        blocks.append([v, 1.0])
        while len(blocks) > 1 and blocks[-2][0] / blocks[-2][1] > blocks[-1][0] / blocks[-1][1]:
            s, c = blocks.pop()
            blocks[-1][0] += s
            blocks[-1][1] += c
    out: list[float] = []
    for s, c in blocks:
        out.extend([s / c] * int(c))
    return out


def pack(labels: list[Label], top: float, bottom: float, side: str = "") -> None:
    """Assign each label a vertical centre, in anchor order, without overlap."""
    if not labels:
        return
    labels.sort(key=lambda l: l.ay)

    # Shift into a space where the spacing constraint becomes "non-decreasing",
    # so plain isotonic regression solves it.
    offsets, acc = [0.0], 0.0
    for a, b in zip(labels, labels[1:]):
        acc += (a.height + b.height) / 2 + GAP
        offsets.append(acc)

    needed = sum(l.height for l in labels) + GAP * (len(labels) - 1)
    available = bottom - top
    if needed > available:
        print(
            f"warning: {len(labels)} labels in the {side or 'side'} gutter need "
            f"{needed:.0f}px of height but only "
            f"{available:.0f}px is available. They will be evenly spread and may "
            f"feel cramped -- consider numbered callouts with a key (--numbered), "
            f"fewer labels, or a taller canvas.",
            file=sys.stderr,
        )
        step = available / len(labels)
        for i, l in enumerate(labels):
            l.y = top + step * (i + 0.5)
        return

    lower = top + labels[0].height / 2
    upper = bottom - labels[-1].height / 2 - offsets[-1]
    targets = [l.ay - o for l, o in zip(labels, offsets)]
    solved = pava(targets, lower, upper)
    for l, z, o in zip(labels, solved, offsets):
        l.y = z + o


def esc(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def render_gutter(labels: list[Label], spec: dict) -> list[str]:
    width = spec["width"]
    gutter = spec["gutter"]
    accent = spec["accent"]
    fs = spec["font_size"]
    out = []
    for l in sorted(labels, key=lambda l: l.index):
        if l.side == "left":
            bend_x = gutter - 4
            text_x = gutter - RUN - TEXT_PAD - 4
            anchor = "end"
        else:
            bend_x = width - gutter + 4
            text_x = width - gutter + RUN + TEXT_PAD + 4
            anchor = "start"
        line_end_x = text_x + (TEXT_PAD if l.side == "left" else -TEXT_PAD)
        first = l.y - (len(l.lines) - 1) * LINE_H / 2 + BASELINE_SHIFT

        out.append(f'    <g class="callout" data-label="{esc(l.lines[0])}">')
        out.append(
            f'      <path class="leader" fill="none" stroke="{accent}" stroke-width="1" '
            f'd="M {l.ax:.1f} {l.ay:.1f} L {bend_x:.1f} {l.y:.1f} L {line_end_x:.1f} {l.y:.1f}"/>'
        )
        out.append(f'      <circle cx="{l.ax:.1f}" cy="{l.ay:.1f}" r="3" fill="{accent}"/>')
        out.append(
            f'      <text class="label" x="{text_x:.1f}" y="{first:.1f}" '
            f'text-anchor="{anchor}" font-size="{fs}" font-weight="500">'
        )
        for i, line in enumerate(l.lines):
            dy = 0 if i == 0 else LINE_H
            out.append(f'        <tspan x="{text_x:.1f}" dy="{dy}">{esc(line)}</tspan>')
        out.append("      </text>")
        out.append("    </g>")
    return out


def render_numbered(labels: list[Label], spec: dict) -> list[str]:
    accent = spec["accent"]
    fs = spec["font_size"]
    width, bottom = spec["width"], spec["bottom"]
    out = []
    for l in sorted(labels, key=lambda l: l.index):
        n = l.index + 1
        r = 11 if n < 10 else 12
        out.append(f'    <g class="callout" data-label="{esc(" ".join(l.lines))}">')
        out.append(f'      <circle cx="{l.ax:.1f}" cy="{l.ay:.1f}" r="{r}" fill="{accent}"/>')
        out.append(
            f'      <text x="{l.ax:.1f}" y="{l.ay + 4.5:.1f}" text-anchor="middle" '
            f'font-size="12" font-weight="600" fill="var(--paper, #FDFCFA)">{n}</text>'
        )
        out.append("    </g>")

    cols = 1 if len(labels) <= 6 else 2
    per_col = -(-len(labels) // cols)
    col_w = (width - 2 * EDGE_PAD - 80) / cols
    key_y = bottom + 34
    key_h = 24 + per_col * 22
    if key_y + key_h > spec["height"]:
        print(
            f"warning: the key needs {key_h}px below y={key_y:.0f} but the canvas ends at "
            f"{spec['height']}. Raise \"bottom\", grow \"height\" to about "
            f"{key_y + key_h + 24:.0f}, or move the key beside the figure by hand.",
            file=sys.stderr,
        )
    out.append(f'    <g id="key" transform="translate({EDGE_PAD + 40}, {key_y:.0f})">')
    out.append(f'      <text class="label" x="0" y="0" font-size="{fs}" font-weight="600">Key</text>')
    for i, l in enumerate(sorted(labels, key=lambda l: l.index)):
        cx, cy = (i // per_col) * col_w, 24 + (i % per_col) * 22
        out.append(f'      <g transform="translate({cx:.0f}, {cy:.0f})">')
        out.append(f'        <circle cx="8" cy="-4" r="8" fill="{accent}"/>')
        out.append(
            f'        <text x="8" y="0" text-anchor="middle" font-size="10" '
            f'font-weight="600" fill="var(--paper, #FDFCFA)">{i + 1}</text>'
        )
        out.append(f'        <text class="label" x="26" y="0" font-size="{fs}">{esc(" ".join(l.lines))}</text>')
        out.append("      </g>")
    out.append("    </g>")
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("spec", help="JSON spec file ('-' for stdin)")
    ap.add_argument("--numbered", action="store_true",
                    help="numbered discs plus a key block, for dense figures")
    args = ap.parse_args()

    raw = sys.stdin.read() if args.spec == "-" else open(args.spec, encoding="utf-8").read()
    spec = json.loads(raw)

    spec.setdefault("width", 1200)
    spec.setdefault("height", 800)
    spec.setdefault("gutter", 240)
    spec.setdefault("top", 88)
    spec.setdefault("bottom", spec["height"] - 64)
    spec.setdefault("accent", "#C2410C")
    spec.setdefault("font_size", 14)

    callouts = spec.get("callouts") or []
    if not callouts:
        print("error: spec has no callouts", file=sys.stderr)
        return 2

    avail_px = spec["gutter"] - RUN - TEXT_PAD - EDGE_PAD - 8
    max_chars = max(8, int(avail_px / (spec["font_size"] * CHAR_W_RATIO)))
    mid = spec["width"] / 2

    labels: list[Label] = []
    for i, c in enumerate(callouts):
        side = c.get("side") or ("left" if c["x"] < mid else "right")
        if side not in ("left", "right"):
            print(f"error: callout {i} has side={side!r}; use 'left' or 'right'", file=sys.stderr)
            return 2
        lines = wrap(str(c.get("text", "")), max_chars)
        labels.append(
            Label(index=i, ax=float(c["x"]), ay=float(c["y"]), lines=lines,
                  side=side, height=len(lines) * LINE_H)
        )

    body: list[str] = []
    if args.numbered:
        body = render_numbered(labels, spec)
    else:
        for side in ("left", "right"):
            group = [l for l in labels if l.side == side]
            pack(group, spec["top"], spec["bottom"], side)
            body += render_gutter(group, spec)

    print('  <g id="annotations">')
    print("\n".join(body))
    print("  </g>")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
