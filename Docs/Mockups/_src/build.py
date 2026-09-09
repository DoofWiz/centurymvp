"""Build standalone Century mockups.

Each fragment in pages/NN-name.html starts with header comments:
    <!-- title: Battle HUD -->
    <!-- desc: one-line description for the index -->
    <!-- world: overmap | battle | camp | title | none -->   (optional, default none)
followed by an optional <style> block and the page markup (placed inside #stage > .hud-root).

Output: Docs/Mockups/NN-name.html (fully self-contained) + index.html gallery.
"""
import re, sys, html
from pathlib import Path

HERE = Path(__file__).parent
OUT = HERE.parent if HERE.name == "_src" else Path(r"D:/Unity/Century Prototype/Docs/Mockups")
COMMON = (HERE / "common.css").read_text(encoding="utf-8")
ICONS = (HERE / "icons.svg").read_text(encoding="utf-8")

SCALER = """<script>
(function(){var s=document.getElementById('stage');function fit(){var w=innerWidth,h=innerHeight,k=Math.min(w/1920,h/1080);
s.style.transform='translate(-50%,-50%) scale('+k+')';s.style.transformOrigin='50% 50%';}
addEventListener('resize',fit);fit();setTimeout(fit,50);setTimeout(fit,400);setTimeout(fit,1500);
if(window.ResizeObserver){new ResizeObserver(fit).observe(document.documentElement);}
addEventListener('keydown',function(e){if(e.key==='n'||e.key==='N')document.body.classList.toggle('notes');});
document.querySelectorAll('[data-tabs]').forEach(function(g){var tabs=g.querySelectorAll('[data-tab]');
tabs.forEach(function(t){t.addEventListener('click',function(){tabs.forEach(function(x){x.classList.remove('pill--active','nav-item--active','tab--active');});
t.classList.add(t.classList.contains('nav-item')?'nav-item--active':(t.classList.contains('tab')?'tab--active':'pill--active'));
var id=t.getAttribute('data-tab');document.querySelectorAll('[data-page]').forEach(function(p){p.hidden=p.getAttribute('data-page')!==id;});});});});
})();
</script>"""

HEAD = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Century · {title}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@400;600;700&family=Cormorant+Garamond:ital,wght@0,400;0,500;0,600;1,400&display=swap" rel="stylesheet">
<style>
{common}
</style>
{pagestyle}
</head>
<body>
{icons}
<div id="stage">
{world}
<div class="hud-root">
{markup}
</div>
</div>
<div class="mock-tag"><a href="index.html">&larr; index</a><span>mockup {num} · {title}</span><span class="text-faint">N = notes</span></div>
{scaler}
</body>
</html>
"""

WORLDS = {
    "overmap": '<div class="world world--overmap"></div><div class="grain"></div>',
    "battle":  '<div class="world world--battle"></div><div class="grain"></div>',
    "camp":    '<div class="world world--camp"></div><div class="grain"></div><div class="vignette"></div>',
    "title":   '<div class="world world--title"></div><div class="grain"></div>',
    "none":    '',
}

def meta(src, key, default=""):
    m = re.search(r"<!--\s*%s:\s*(.*?)\s*-->" % key, src)
    return m.group(1).strip() if m else default

def build_page(path):
    src = path.read_text(encoding="utf-8")
    title = meta(src, "title", path.stem)
    desc = meta(src, "desc", "")
    world = meta(src, "world", "none")
    num = path.stem.split("-")[0]
    body = re.sub(r"<!--\s*(title|desc|world):.*?-->\s*", "", src, flags=re.S)
    style = ""
    m = re.search(r"<style>.*?</style>", body, flags=re.S)
    if m:
        style = m.group(0)
        body = body[:m.start()] + body[m.end():]
    out = HEAD.format(title=html.escape(title), common=COMMON, pagestyle=style, icons=ICONS,
                      world=WORLDS.get(world, ""), markup=body.strip(), num=num, scaler=SCALER)
    (OUT / path.name).write_text(out, encoding="utf-8")
    return dict(file=path.name, title=title, desc=desc, num=num)

INDEX = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Century · Mockups</title>
<link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@600;700&display=swap" rel="stylesheet">
<style>
{common}
html, body {{ overflow: auto; height: auto; }}
body {{ padding: 40px 48px 80px; }}
h1 {{ font-family: var(--font-display); font-variant: small-caps; color: #f0e6cb; letter-spacing: 8px; font-weight: 700; font-size: 34px; margin: 0; text-transform: uppercase; }}
.sub {{ color: #cbb98d; letter-spacing: 3px; font-size: 12px; margin: 6px 0 30px; text-transform: uppercase; }}
.grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(420px, 1fr)); gap: 22px; }}
.tile {{ display: block; text-decoration: none; background: rgba(40,30,16,.8); border: 1px solid rgba(154,123,58,.4); transition: border-color .12s; }}
.tile:hover {{ border-color: #cbb98d; }}
.thumb {{ position: relative; width: 100%; aspect-ratio: 16/9; overflow: hidden; background: #b3a276; border-bottom: 1px solid rgba(154,123,58,.4); }}
.thumb iframe {{ position: absolute; left: 0; top: 0; width: 1920px; height: 1080px; border: 0; transform-origin: 0 0; pointer-events: none; }}
.cap {{ padding: 10px 14px 12px; }}
.cap .n {{ font-size: 10px; color: #9a7b3a; letter-spacing: 3px; }}
.cap .t {{ font-size: 16px; color: #e6dcc3; letter-spacing: 2px; margin-top: 2px; text-transform: uppercase; }}
.cap .d {{ font-size: 12px; color: #b7a883; margin-top: 4px; line-height: 1.4; }}
.hint {{ color: #8f7f5e; font-size: 11px; letter-spacing: 1px; margin-top: 34px; }}
</style></head><body>
<h1>Century</h1>
<div class="sub">UI mockups · throwaway HTML · not production code</div>
<div class="grid">
{tiles}
</div>
<div class="hint">Every page scales a 1920×1080 stage to your window. Press N on any page to toggle designer notes. Tabs and pills that switch panels are clickable; everything else is static.</div>
<script>
function fit(){{document.querySelectorAll('.thumb').forEach(function(t){{var k=t.clientWidth/1920;t.querySelector('iframe').style.transform='scale('+k+')';}});}}
addEventListener('resize',fit);addEventListener('load',fit);fit();
</script>
</body></html>
"""

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    pages = sorted((HERE / "pages").glob("*.html"))
    infos = [build_page(p) for p in pages]
    tiles = "\n".join(
        '<a class="tile" href="{f}"><div class="thumb"><iframe src="{f}" tabindex="-1" loading="lazy"></iframe></div>'
        '<div class="cap"><div class="n">{n}</div><div class="t">{t}</div><div class="d">{d}</div></div></a>'.format(
            f=i["file"], n=i["num"], t=html.escape(i["title"]), d=html.escape(i["desc"])) for i in infos)
    (OUT / "index.html").write_text(INDEX.format(common=COMMON, tiles=tiles), encoding="utf-8")
    for i in infos:
        print(f'{i["num"]}  {i["title"]}')
    print(f"{len(infos)} pages -> {OUT}")

if __name__ == "__main__":
    main()
