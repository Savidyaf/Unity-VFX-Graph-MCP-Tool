#!/usr/bin/env python3
"""Sync package VFX MCP tools into Assets compatibility path.

Package path is the canonical source of truth:
  Packages/com.pakaya.mcp.vfx/Editor/Tools/Vfx

Assets path is maintained as a compatibility mirror:
  Assets/MCPForUnity/Editor/Tools/Vfx
"""

from __future__ import annotations

import argparse
import hashlib
import shutil
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PACKAGE_VFX = REPO_ROOT / "Packages/com.pakaya.mcp.vfx/Editor/Tools/Vfx"
ASSETS_VFX = REPO_ROOT / "Assets/MCPForUnity/Editor/Tools/Vfx"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _iter_cs_files(path: Path) -> list[Path]:
    return sorted(p for p in path.glob("*.cs") if p.is_file())


def _mirror_meta(src_cs: Path, dst_cs: Path) -> None:
    src_meta = src_cs.with_suffix(src_cs.suffix + ".meta")
    dst_meta = dst_cs.with_suffix(dst_cs.suffix + ".meta")
    if src_meta.exists():
        shutil.copy2(src_meta, dst_meta)


def check_sync(verbose: bool = True) -> int:
    package_files = _iter_cs_files(PACKAGE_VFX)
    if not package_files:
        print("No package VFX files found; nothing to check.")
        return 0

    package_names = {f.name for f in package_files}

    mismatches: list[str] = []
    for package_file in package_files:
        target_file = ASSETS_VFX / package_file.name
        if not target_file.exists():
            mismatches.append(f"missing: {target_file.relative_to(REPO_ROOT)}")
            continue
        if _sha256(package_file) != _sha256(target_file):
            mismatches.append(
                f"content mismatch: {package_file.relative_to(REPO_ROOT)} -> {target_file.relative_to(REPO_ROOT)}"
            )

    if ASSETS_VFX.exists():
        for asset_file in _iter_cs_files(ASSETS_VFX):
            if asset_file.name not in package_names:
                mismatches.append(f"orphan (not in package): {asset_file.relative_to(REPO_ROOT)}")

    if mismatches:
        print("VFX tool mirror drift detected:")
        for entry in mismatches:
            print(f"- {entry}")
        print("Run: python3 scripts/sync_vfx_tools.py --write")
        return 1

    if verbose:
        print("VFX package and Assets compatibility mirror are in sync.")
    return 0


def write_sync() -> int:
    ASSETS_VFX.mkdir(parents=True, exist_ok=True)
    package_files = _iter_cs_files(PACKAGE_VFX)
    package_names = {f.name for f in package_files}
    copied = 0

    for package_file in package_files:
        target_file = ASSETS_VFX / package_file.name
        shutil.copy2(package_file, target_file)
        _mirror_meta(package_file, target_file)
        copied += 1

    removed = 0
    for asset_file in _iter_cs_files(ASSETS_VFX):
        if asset_file.name not in package_names:
            asset_file.unlink()
            meta = asset_file.with_suffix(asset_file.suffix + ".meta")
            if meta.exists():
                meta.unlink()
            removed += 1

    print(f"Synced {copied} VFX tool files from package to Assets compatibility path.")
    if removed:
        print(f"Removed {removed} orphaned file(s) from Assets mirror.")
    return check_sync(verbose=False)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--write",
        action="store_true",
        help="Copy canonical package VFX tool files to Assets compatibility path.",
    )
    args = parser.parse_args(argv)

    if args.write:
        return write_sync()
    return check_sync(verbose=True)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
