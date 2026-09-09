"""Lint mockup fragments: unknown icon ids, unknown CSS classes, stray html/head/body, missing meta."""
import re
from pathlib import Path

HERE = Path(__file__).parent
common = (HERE / "common.css").read_text(encoding="utf-8")
icons = set(re.findall(r'<symbol id="([^"]+)"', (HERE / "icons.svg").read_text(encoding="utf-8")))

def classes_in_css(css):
    return set(re.findall(r"\.([A-Za-z_][\w-]*)", re.sub(r"/\*.*?\*/", "", css, flags=re.S)))

common_classes = classes_in_css(common)
problems = 0
for path in sorted((HERE / "pages").glob("*.html")):
    src = path.read_text(encoding="utf-8")
    issues = []
    for key in ("title", "desc", "world"):
        if not re.search(r"<!--\s*%s:" % key, src):
            issues.append(f"missing <!-- {key}: -->")
    if re.search(r"<(html|head|body)\b", src):
        issues.append("contains html/head/body tag")
    if re.search(r'<link|<script src|url\(\s*["\']?https?:', src):
        issues.append("references an external resource")
    style = "".join(re.findall(r"<style>(.*?)</style>", src, flags=re.S))
    page_classes = classes_in_css(style)
    used = set()
    for attr in re.findall(r'class="([^"]*)"', src):
        used.update(attr.split())
    unknown = sorted(c for c in used if c not in common_classes and c not in page_classes)
    if unknown:
        issues.append("unknown classes: " + ", ".join(unknown))
    refs = set(re.findall(r'href="#([^"]+)"', src))
    bad = sorted(r for r in refs if r not in icons)
    if bad:
        issues.append("unknown icon ids: " + ", ".join(bad))
    notes = len(re.findall(r'class="mock-note', src))
    if notes < 2:
        issues.append(f"only {notes} mock-note(s)")
    lines = src.count("\n")
    status = "OK " if not issues else "!! "
    print(f"{status}{path.name:36s} {lines:4d} lines" + ("" if not issues else "\n      - " + "\n      - ".join(issues)))
    problems += len(issues)
print(f"\n{problems} problem(s)")
