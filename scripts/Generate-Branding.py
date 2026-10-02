# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 MoriTeahouse (森之宿茶室)
"""Generate original vector tea emblems and Windows icons. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse

engine = Path(__file__).resolve().parents[1]

def create(folder, name, rhythm=False):
    folder.mkdir(parents=True, exist_ok=True)
    ink, cream, mint, gold = "#102c2a", "#f4ead4", "#83d6b2", "#e4b36c"
    accents = ["#83d6b2", "#83b5e4", "#c79ddd", "#e4b36c"]
    bars = "".join(f'<rect x="{156+i*55}" y="{98+(i%2)*14}" width="18" height="{75-(i%2)*14}" rx="9" fill="{c}"/>' for i,c in enumerate(accents)) if rhythm else '<path d="M220 179 Q177 125 224 78 Q271 128 220 179" fill="#83d6b2"/><path d="M223 148 Q279 87 328 111 Q309 172 244 174" fill="#e4b36c"/>'
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512"><title>{name} — MoriTeahouse</title><rect x="8" y="8" width="496" height="496" rx="112" fill="{ink}"/><circle cx="256" cy="256" r="217" fill="none" stroke="{gold}" stroke-width="8" opacity=".75"/>{bars}<path d="M142 214 H347 V278 Q347 354 270 354 H219 Q142 354 142 278 Z" fill="{cream}"/><path d="M347 228 H363 Q410 228 410 267 Q410 312 348 312" fill="none" stroke="{cream}" stroke-width="22"/><ellipse cx="244" cy="218" rx="102" ry="16" fill="{mint}"/><path d="M137 376 H365" fill="none" stroke="{gold}" stroke-width="14" stroke-linecap="round"/><path d="M181 405 Q252 433 326 405" fill="none" stroke="{mint}" stroke-width="7" stroke-linecap="round"/></svg>'
    (folder/(name+".svg")).write_text(svg, encoding="utf-8")
    image = Image.new("RGBA",(512,512),(0,0,0,0)); draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((8,8,504,504),112,fill=ink)
    draw.ellipse((39,39,473,473),outline=gold,width=8)
    if rhythm:
        for i,c in enumerate(accents): draw.rounded_rectangle((156+i*55,98+i%2*14,174+i*55,173),9,fill=c)
    else:
        draw.polygon([(220,179),(200,149),(196,119),(224,78),(248,112),(246,144)],fill=mint)
        draw.polygon([(233,172),(251,143),(279,122),(328,111),(313,146),(284,168)],fill=gold)
    draw.rounded_rectangle((321,221,410,314),40,outline=cream,width=22)
    draw.rectangle((142,214,347,278),fill=cream)
    draw.rounded_rectangle((142,232,347,354),72,fill=cream)
    draw.ellipse((142,202,347,234),fill=mint)
    draw.line((137,376,365,376),fill=gold,width=14)
    draw.arc((176,370,332,430),0,180,fill=mint,width=7)
    image.save(folder/(name+".png"))
    image.save(folder/(name+".ico"),sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])

create(engine/"branding","matrixtea")
parser=argparse.ArgumentParser()
parser.add_argument("--rhythm-root",type=Path)
parser.add_argument("--echo-root",type=Path)
args=parser.parse_args()
if args.rhythm_root: create(args.rhythm_root/"ClickerGame","icon",True)
if args.echo_root: create(args.echo_root/"Assets"/"Art","game-icon")
