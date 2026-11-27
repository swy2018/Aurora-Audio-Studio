"""Validate exported MusicXML offline and render with an independent notation engine."""
import argparse
import base64
import json
import shutil
import tempfile
from pathlib import Path
from lxml import etree
import verovio


def check(source, schemas, output):
    class LocalSchema(etree.Resolver):
        def resolve(self, url, public_id, context):
            name = url.replace("\\", "/").rsplit("/", 1)[-1]
            if name in ("musicxml.xsd", "xml.xsd", "xlink.xsd"):
                return self.resolve_string((schemas / name).read_bytes(), context)
            raise ValueError("Unexpected external schema: " + url)
    parser = etree.XMLParser(no_network=True, resolve_entities=False)
    parser.resolvers.add(LocalSchema())
    schema = etree.XMLSchema(etree.parse(str(schemas / "musicxml.xsd"), parser))
    # Do not give arbitrary document URLs to the schema resolver.
    document = etree.fromstring(source.read_bytes(), etree.XMLParser(no_network=True, resolve_entities=False))
    schema.assertValid(document)
    # The native renderer's Windows resource loader does not accept this
    # workstation's CJK project path. Keep the existing verified resources and
    # copy only those assets to a task-owned ASCII temp path for this check.
    fonts = Path(tempfile.mkdtemp(prefix="AuroraScoreFonts-")) / "data"
    shutil.copytree(Path(verovio.__file__).parent / "data", fonts)
    verovio.setDefaultResourcePath(str(fonts))
    toolkit = verovio.toolkit()
    toolkit.setInputFrom("musicxml")
    toolkit.setOptions(dict(scale=40, pageWidth=2100, pageHeight=2970, breaks="auto"))
    if not toolkit.loadData(source.read_text(encoding="utf-8-sig")) or toolkit.getPageCount() == 0:
        raise RuntimeError("Independent notation renderer rejected MusicXML")
    output.mkdir(parents=True, exist_ok=True)
    music_font_css = (fonts / "Leipzig.css").read_text(encoding="utf-8")
    for page in range(1, toolkit.getPageCount() + 1):
        svg = etree.fromstring(toolkit.renderToSVG(page).encode("utf-8"))
        style = etree.Element("{http://www.w3.org/2000/svg}style")
        style.text = music_font_css
        svg.insert(0, style)
        (output / f"page-{page}.svg").write_bytes(etree.tostring(svg, encoding="utf-8"))
    (output / "renderer-roundtrip.mid").write_bytes(base64.b64decode(toolkit.renderToMIDI()))
    result = dict(schema="MusicXML 4.0", schema_valid=True, renderer=toolkit.getVersion(), pages=toolkit.getPageCount(), source=str(source), temporary_font_copy=str(fonts))
    (output / "validation.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("source", type=Path)
    p.add_argument("schemas", type=Path)
    p.add_argument("output", type=Path)
    a = p.parse_args()
    check(a.source, a.schemas, a.output)
