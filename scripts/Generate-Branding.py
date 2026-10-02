# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 MoriTeahouse (森之宿茶室)
"""Render authoritative README SVGs to PNG/BMP and seven-size Windows ICO.
Requires Windows, .NET 8 SDK and Python 3. SVG sources remain unchanged.
"""
from pathlib import Path
import argparse
import subprocess

engine = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--rhythm-root", type=Path)
parser.add_argument("--echo-root", type=Path)
args = parser.parse_args()
icons = [engine / "branding" / "matrixtea"]
if args.rhythm_root:
    icons.append(args.rhythm_root.resolve() / "ClickerGame" / "icon")
if args.echo_root:
    icons.append(args.echo_root.resolve() / "Assets" / "Art" / "game-icon")
for icon in icons:
    if not icon.with_suffix(".svg").is_file():
        raise SystemExit(f"SVG source does not exist: {icon.with_suffix('.svg')}")
    subprocess.run(["dotnet", "run", "--project", str(engine / "tools" / "MatrixTea.IconTool"),
                    "-c", "Release", "--", str(icon.with_suffix(".svg")), str(icon)], check=True)
