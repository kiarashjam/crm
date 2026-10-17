#!/usr/bin/env python3
"""Lint a finished annotated illustration.

Catches the geometry and hygiene problems that are hard to see by eye but
obvious once the figure is in front of someone else:

  * text that falls outside the viewBox (silently cropped by most renderers)
  * label boxes that overlap each other
  * leader lines that cross, run on top of one another, or start from anchors
    so close together that the reader cannot tell which dot each label means
  * more than three font sizes, which reads as accidental rather than designed
  * the accent colour leaking into the subject, which destroys the "subject vs.
    what we're saying about it" split that makes the accent work at all
  * a missing <title>/<desc>, without which the figure is opaque to screen
    readers and to anyone searching the file

It is a lint, not a renderer: text extents are estimated from character counts,
so the overlap checks are approximate and deliberately a little forgiving.

Usage:
    python3 check_illustration.py figure.svg [--strict] [--quiet]

Exits 1 if any error is found, or if --strict is given and there are warnings.
"""

from __future__ import annotations

import argparse
import math
import re
import sys
import xml.etree.ElementTree as ET

SVG_NS = "http://www.w3.org/2000/svg"
CHAR_W_RATIO = 0.55
MIN_TEXT_GAP = 2.0          # px of slack before two labels count as overlapping
MIN_ANCHOR_GAP = 18.0       # px between two anchor dots before they read as one
ACCENT_LIKE = re.compile(r"#[0-9a-fA-F]{3,8}|rgb\(|var\(--accent")


def tag(el) -> str:
    return el.tag.split("}")[-1]


def parse_translate(transform: str) -> tuple[float, float]:
    dx = dy = 0.0
    for fn, args in re.findall(r"(\w+)\s*\(([^)]*)\)", transform or ""):
        nums = [float(n) for n in re.findall(r"-?\d*\.?\d+(?:e-?\d+)?", args)]
        if fn == "translate" and nums:
            dx += nums[0]
            dy += nums[1] if len(nums) > 1 else 0.0
        elif fn == "matrix" and len(nums) == 6:
            dx += nums[4]
            dy += nums[5]
    return dx, dy


def css_font_sizes(root) -> dict[str, float]:
    """Pull `.cls { font-size: Npx }` out of any <style> element."""
    sizes: dict[str, float] = {}
    for st in root.iter(f"{{{SVG_NS}}}style"):
        text = "".join(st.itertext())
        for sel, body in re.findall(r"([^{}]+)\{([^{}]*)\}", text):
            m = re.search(r"font-size\s*:\s*(\d*\.?\d+)px", body)
            if not m:
                continue
            for part in sel.split(","):
                part = part.strip()
                if part.startswith("."):
                    sizes[part[1:]] = float(m.group(1))
    return sizes


def walk(el, dx=0.0, dy=0.0, ancestors=()):
    """Yield (element, absolute dx, dy, tuple of ancestor ids)."""
    tdx, tdy = parse_translate(el.get("transform", ""))
    dx, dy = dx + tdx, dy + tdy
    here = ancestors + ((el.get("id") or "",) if el.get("id") else ())
    yield el, dx, dy, here
    for child in el:
        yield from walk(child, dx, dy, here)


def text_boxes(root, css: dict[str, float]):
    boxes = []
    for el, dx, dy, anc in walk(root):
        if tag(el) != "text":
            continue
        cls = (el.get("class") or "").split()
        fs = el.get("font-size")
        size = float(re.sub(r"[^\d.]", "", fs)) if fs else next(
            (css[c] for c in cls if c in css), 14.0
        )
        spans = [s for s in el.iter(f"{{{SVG_NS}}}tspan")]
        if spans:
            lines = ["".join(s.itertext()).strip() for s in spans]
            x = float(spans[0].get("x", el.get("x", 0)) or 0)
        else:
            lines = ["".join(el.itertext()).strip()]
            x = float(el.get("x", 0) or 0)
        y = float(el.get("y", 0) or 0)
        lines = [l for l in lines if l]
        if not lines:
            continue
        w = max(len(l) for l in lines) * size * CHAR_W_RATIO
        h = len(lines) * size * 1.22
        anchor = el.get("text-anchor", "start")
        left = x + dx - (w if anchor == "end" else w / 2 if anchor == "middle" else 0)
        top = y + dy - size
        boxes.append({
            "text": " / ".join(lines), "size": size, "group": anc,
            "x0": left, "y0": top, "x1": left + w, "y1": top + h,
        })
    return boxes


def path_polylines(root):
    """Leader paths as point lists. Curves are sampled at their control hull."""
    out = []
    for el, dx, dy, anc in walk(root):
        if tag(el) != "path":
            continue
        classes = (el.get("class") or "")
        if "leader" not in classes:
            continue
        d = el.get("d", "")
        nums = re.findall(r"-?\d*\.?\d+(?:e-?\d+)?", d)
        if len(nums) < 4:
            continue
        coords = [float(n) for n in nums]
        pts = [(coords[i] + dx, coords[i + 1] + dy) for i in range(0, len(coords) - 1, 2)]
        if len(pts) >= 2:
            out.append({"pts": pts, "group": anc, "curved": bool(re.search(r"[CcQqSsTtAa]", d))})
    return out


def seg_cross(p1, p2, p3, p4) -> bool:
    def o(a, b, c):
        v = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        return 0 if abs(v) < 1e-9 else (1 if v > 0 else -1)
    o1, o2, o3, o4 = o(p1, p2, p3), o(p1, p2, p4), o(p3, p4, p1), o(p3, p4, p2)
    return o1 != o2 and o3 != o4 and o1 != 0 and o2 != 0 and o3 != 0 and o4 != 0


def seg_overlap(p1, p2, p3, p4, tol=4.0, min_len=10.0) -> bool:
    """True when two segments are near-parallel, within `tol` px, and share a run.

    Crossing is not the only way two leaders become unreadable: laid along the
    same line, they merge into one stroke and the eye cannot tell which label
    belongs to which dot.
    """
    ax, ay = p2[0] - p1[0], p2[1] - p1[1]
    bx, by = p4[0] - p3[0], p4[1] - p3[1]
    la, lb = math.hypot(ax, ay), math.hypot(bx, by)
    if la < min_len or lb < min_len:
        return False
    if abs(ax * by - ay * bx) / (la * lb) > math.sin(math.radians(4)):
        return False                       # not parallel
    ux, uy = ax / la, ay / la
    dist = abs((p3[0] - p1[0]) * uy - (p3[1] - p1[1]) * ux)
    if dist > tol:
        return False                       # parallel but apart
    t3 = (p3[0] - p1[0]) * ux + (p3[1] - p1[1]) * uy
    t4 = (p4[0] - p1[0]) * ux + (p4[1] - p1[1]) * uy
    shared = min(la, max(t3, t4)) - max(0.0, min(t3, t4))
    return shared > min_len


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("svg")
    ap.add_argument("--strict", action="store_true", help="treat warnings as failures")
    ap.add_argument("--quiet", action="store_true", help="print only problems")
    args = ap.parse_args()

    try:
        root = ET.parse(args.svg).getroot()
    except ET.ParseError as e:
        print(f"error: {args.svg} is not well-formed XML: {e}")
        return 1

    errors: list[str] = []
    warnings: list[str] = []

    # --- accessibility ---------------------------------------------------
    kids = [tag(c) for c in root]
    if "title" not in kids:
        errors.append("no <title> child on the root <svg>; the figure has no name")
    if "desc" not in kids:
        warnings.append("no <desc> child on the root <svg>; add the one-sentence "
                        "explanation the figure exists to deliver")
    if not root.get("aria-labelledby"):
        warnings.append('root <svg> has no aria-labelledby pointing at the title/desc ids')
    if root.get("role") != "img":
        warnings.append('root <svg> has no role="img"')

    # --- viewBox ---------------------------------------------------------
    vb = root.get("viewBox")
    if not vb:
        errors.append("root <svg> has no viewBox, so the figure will not scale")
        vx = vy = 0.0
        vw = float(re.sub(r"[^\d.]", "", root.get("width", "1200")) or 1200)
        vh = float(re.sub(r"[^\d.]", "", root.get("height", "800")) or 800)
    else:
        vx, vy, vw, vh = [float(n) for n in re.split(r"[\s,]+", vb.strip())[:4]]

    css = css_font_sizes(root)
    boxes = text_boxes(root, css)
    if not boxes:
        warnings.append("no <text> elements found -- are the labels outlined as paths? "
                        "Keep them as text so they stay searchable and translatable")

    # --- text inside the frame -------------------------------------------
    for b in boxes:
        if b["x0"] < vx - 1 or b["y0"] < vy - 1 or b["x1"] > vx + vw + 1 or b["y1"] > vy + vh + 1:
            errors.append(
                f'text {b["text"]!r} falls outside the viewBox '
                f'(x {b["x0"]:.0f}..{b["x1"]:.0f}, y {b["y0"]:.0f}..{b["y1"]:.0f})'
            )

    # --- overlapping labels ----------------------------------------------
    for i, a in enumerate(boxes):
        for b in boxes[i + 1:]:
            ox = min(a["x1"], b["x1"]) - max(a["x0"], b["x0"])
            oy = min(a["y1"], b["y1"]) - max(a["y0"], b["y0"])
            if ox > MIN_TEXT_GAP and oy > MIN_TEXT_GAP:
                errors.append(
                    f'labels {a["text"]!r} and {b["text"]!r} overlap by '
                    f'{ox:.0f}x{oy:.0f}px'
                )

    # --- crossing leaders -------------------------------------------------
    leaders = path_polylines(root)
    if any(l["curved"] for l in leaders):
        warnings.append("some leader lines use curves; straight leaders with a single "
                        "elbow are easier to follow, and the crossing check below only "
                        "approximates curved ones")
    crossings = 0
    for i, la in enumerate(leaders):
        for lb in leaders[i + 1:]:
            hit = False
            for p1, p2 in zip(la["pts"], la["pts"][1:]):
                for p3, p4 in zip(lb["pts"], lb["pts"][1:]):
                    if seg_cross(p1, p2, p3, p4):
                        hit = True
                        break
                if hit:
                    break
            if hit:
                crossings += 1
    overlaps = 0
    for i, la in enumerate(leaders):
        for lb in leaders[i + 1:]:
            if any(seg_overlap(p1, p2, p3, p4)
                   for p1, p2 in zip(la["pts"], la["pts"][1:])
                   for p3, p4 in zip(lb["pts"], lb["pts"][1:])):
                overlaps += 1
    close = []
    for i, la in enumerate(leaders):
        for lb in leaders[i + 1:]:
            (x1, y1), (x2, y2) = la["pts"][0], lb["pts"][0]
            d = math.hypot(x1 - x2, y1 - y2)
            if d < MIN_ANCHOR_GAP:
                close.append(f"({x1:.0f},{y1:.0f}) and ({x2:.0f},{y2:.0f}), {d:.0f}px apart")
    for c in close:
        errors.append(f"anchors {c}: their leaders fan out from what reads as one dot; "
                      f"anchor each callout on a distinct part of the drawing")
    if overlaps:
        errors.append(f"{overlaps} pair(s) of leader lines run on top of each other; "
                      f"anchor them further apart, or on different parts of the feature")
    if crossings:
        errors.append(f"{crossings} pair(s) of leader lines cross; send each anchor to "
                      f"the nearer gutter and keep label order matching anchor order "
                      f"(scripts/layout_callouts.py does this)")

    # --- type scale -------------------------------------------------------
    sizes = sorted({round(b["size"], 1) for b in boxes})
    if len(sizes) > 3:
        warnings.append(f"{len(sizes)} font sizes in use ({', '.join(str(s) for s in sizes)}); "
                        f"three is plenty -- label, caption, title")

    # --- accent leaking into the subject ----------------------------------
    annotation_colours: set[str] = set()
    subject_colours: set[str] = set()
    for el, _dx, _dy, anc in walk(root):
        in_ann = "annotations" in anc
        in_sub = "subject" in anc
        if not (in_ann or in_sub):
            continue
        for attr in ("stroke", "fill"):
            v = (el.get(attr) or "").strip()
            if v and v != "none" and ACCENT_LIKE.match(v):
                (annotation_colours if in_ann else subject_colours).add(v.lower())
    leaked = annotation_colours & subject_colours
    if leaked:
        warnings.append(
            f"colour(s) {', '.join(sorted(leaked))} appear in both #annotations and "
            f"#subject; keeping the accent exclusive to the annotation layer is what "
            f"lets the reader separate the thing from the commentary"
        )
    if not any("annotations" in (el.get("id") or "") for el in root.iter()):
        warnings.append('no <g id="annotations"> group; grouping subject / annotations / '
                        'frame separately makes the figure restylable later')

    # --- report -----------------------------------------------------------
    for e in errors:
        print(f"error: {e}")
    for w in warnings:
        print(f"warning: {w}")
    if not errors and not warnings and not args.quiet:
        print(f"{args.svg}: clean -- {len(boxes)} labels, {len(leaders)} leaders, no crossings")
    elif not args.quiet:
        print(f"\n{args.svg}: {len(errors)} error(s), {len(warnings)} warning(s)")

    return 1 if errors or (args.strict and warnings) else 0


if __name__ == "__main__":
    raise SystemExit(main())
