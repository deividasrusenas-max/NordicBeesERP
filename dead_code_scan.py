#!/usr/bin/env python3
"""
Dead code scanner for NordicBeesERP (.NET / Blazor).

Pure regex + reference counting - no LLM involved, so results are exact
(no hallucination), though heuristic (regex, not a real Roslyn parse), so
always eyeball the report before deleting anything.

Finds:
  1. Public classes/methods never referenced outside their declaring file
  2. Private methods never called anywhere in their file
  3. [Inject] services declared but never used in the same .razor/.cs file
  4. Razor components (.razor) never referenced elsewhere (as a tag <Name .../>
     or as a generic type argument, e.g. DialogService.ShowAsync<Name>(...))

Usage:
    python3 dead_code_scan.py /path/to/NordicBeesERP > dead_code_report.md

No files are modified. Report only.
"""

import os
import re
import sys
from collections import defaultdict

EXCLUDE_DIRS = {"bin", "obj", ".git", ".vs", "node_modules", "Migrations"}
CODE_EXTS = {".cs", ".razor"}

CLASS_RE = re.compile(
    r'^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(\w+)', re.MULTILINE)

# crude but workable: "public <modifiers> <ReturnType> Name(" - excludes constructors
# by requiring a return-type token before the name (constructors have no return type).
PUBLIC_METHOD_RE = re.compile(
    r'^\s*public\s+(?:static\s+|virtual\s+|override\s+|async\s+|abstract\s+|sealed\s+)*'
    r'(?:[\w<>\[\],\.\?]+)\s+(\w+)\s*\([^;{]*\)\s*(?:{|=>)', re.MULTILINE)

PRIVATE_METHOD_RE = re.compile(
    r'^\s*private\s+(?:static\s+|async\s+|readonly\s+)*'
    r'(?:[\w<>\[\],\.\?]+)\s+(\w+)\s*\([^;{]*\)\s*(?:{|=>)', re.MULTILINE)

INJECT_RE = re.compile(
    r'\[Inject\]\s*(?:public\s+)?[\w<>\[\],\.\?]+\??\s+(\w+)\s*{\s*get;', re.MULTILINE)


def walk_files(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for f in filenames:
            ext = os.path.splitext(f)[1]
            if ext in CODE_EXTS:
                yield os.path.join(dirpath, f)


def line_of(text, idx):
    return text.count("\n", 0, idx) + 1


def word_count_excluding(text, name, exclude_start, exclude_end):
    """Count \bname\b occurrences in text, excluding the span [exclude_start, exclude_end)."""
    pattern = re.compile(r'\b' + re.escape(name) + r'\b')
    count = 0
    for m in pattern.finditer(text):
        if exclude_start <= m.start() < exclude_end:
            continue
        count += 1
    return count


def main():
    if len(sys.argv) < 2:
        print("Usage: python3 dead_code_scan.py /path/to/NordicBeesERP", file=sys.stderr)
        sys.exit(1)

    root = sys.argv[1]
    files = list(walk_files(root))

    # Cache file contents once.
    contents = {}
    for fp in files:
        try:
            with open(fp, "r", encoding="utf-8", errors="ignore") as fh:
                contents[fp] = fh.read()
        except OSError:
            continue

    all_text_joined = None  # built lazily per-symbol search across files

    findings = defaultdict(list)  # category -> list of (file, line, name, reason)

    # ---- 1 & 2: public/private classes & methods ----
    for fp, text in contents.items():
        if not fp.endswith(".cs"):
            continue
        rel = os.path.relpath(fp, root)

        for m in CLASS_RE.finditer(text):
            name = m.group(1)
            ln = line_of(text, m.start())
            external_hits = 0
            for other_fp, other_text in contents.items():
                if other_fp == fp:
                    external_hits += word_count_excluding(text, name, m.start(1), m.end(1))
                else:
                    external_hits += len(re.findall(r'\b' + re.escape(name) + r'\b', other_text))
            if external_hits == 0:
                findings["1_public_unreferenced"].append(
                    (rel, ln, name, "public class - no references found anywhere else in the codebase"))

        for m in PUBLIC_METHOD_RE.finditer(text):
            name = m.group(1)
            if name in ("if", "for", "foreach", "while", "switch", "using", "lock"):
                continue
            ln = line_of(text, m.start())
            external_hits = 0
            same_file_hits = word_count_excluding(text, name, m.start(1), m.end(1))
            for other_fp, other_text in contents.items():
                if other_fp == fp:
                    continue
                external_hits += len(re.findall(r'\b' + re.escape(name) + r'\b', other_text))
            if external_hits == 0:
                reason = ("public method - not referenced in any other file"
                          + (" (also unused within its own file)" if same_file_hits == 0 else
                             " (only called from within its own file - check if it should be private)"))
                findings["1_public_unreferenced"].append((rel, ln, name, reason))

        for m in PRIVATE_METHOD_RE.finditer(text):
            name = m.group(1)
            ln = line_of(text, m.start())
            same_file_hits = word_count_excluding(text, name, m.start(1), m.end(1))
            if same_file_hits == 0:
                findings["2_private_unused"].append(
                    (rel, ln, name, "private method - never called anywhere in this file"))

    # ---- 3: [Inject] services declared but unused ----
    for fp, text in contents.items():
        rel = os.path.relpath(fp, root)
        for m in INJECT_RE.finditer(text):
            name = m.group(1)
            ln = line_of(text, m.start())
            same_file_hits = word_count_excluding(text, name, m.start(1), m.end(1))
            if same_file_hits == 0:
                findings["3_unused_injected_services"].append(
                    (rel, ln, name, "[Inject] property - never referenced elsewhere in this file"))

    # ---- 4: Razor components never rendered ----
    razor_files = [fp for fp in contents if fp.endswith(".razor")]
    for fp in razor_files:
        rel = os.path.relpath(fp, root)
        component_name = os.path.splitext(os.path.basename(fp))[0]
        if component_name in ("_Imports", "App", "Routes", "MainLayout"):
            continue
        hits = 0
        for other_fp, other_text in contents.items():
            if other_fp == fp:
                continue
            # tag usage: <ComponentName ...> or <ComponentName/>
            if re.search(r'<' + re.escape(component_name) + r'\b', other_text):
                hits += 1
            # generic type usage: ShowAsync<ComponentName>, typeof(ComponentName), etc.
            if re.search(r'<\s*' + re.escape(component_name) + r'\s*[>,]', other_text):
                hits += 1
            if re.search(r'\btypeof\(\s*' + re.escape(component_name) + r'\s*\)', other_text):
                hits += 1
        if hits == 0:
            findings["4_unused_razor_components"].append(
                (rel, 1, component_name, "no <Tag> usage, generic-type usage, or typeof() reference found in any other file"))

    # ---- Report ----
    titles = {
        "1_public_unreferenced": "1. Public methods/classes never referenced outside their own file",
        "2_private_unused": "2. Private methods never called",
        "3_unused_injected_services": "3. Injected services declared but never used",
        "4_unused_razor_components": "4. Razor components never rendered anywhere",
    }
    print(f"# Dead code report for {root}\n")
    for key in ["1_public_unreferenced", "2_private_unused", "3_unused_injected_services", "4_unused_razor_components"]:
        items = findings[key]
        print(f"\n## {titles[key]} ({len(items)} found)\n")
        if not items:
            print("_none found_\n")
            continue
        for rel, ln, name, reason in sorted(items, key=lambda x: (x[0], x[1])):
            print(f"- `{rel}:{ln}` **{name}** - {reason}")

    print("\n\n---\nThis is a regex-based heuristic scan, not a real Roslyn/semantic analysis.\n"
          "False positives are possible with reflection, DI-by-interface, JSON deserialization\n"
          "targets, EF navigation properties, or names that collide with common words.\n"
          "Verify each finding before deleting anything.")


if __name__ == "__main__":
    main()
