"""Opt-in, serial real-model acceptance with isolated outputs and metadata."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid

import numpy as np
import pretty_midi
import soundfile as sf


def main():
    scripts = Path(__file__).resolve().parent
    project = scripts.parents[3]
    installed = Path.home() / "Aurora" / "Models"
    root = project / ".local-check" / ("native-parity-live-" + uuid.uuid4().hex)
    root.mkdir(parents=True)
    catalog = json.loads((scripts / "catalog.json").read_text())
    families = {"roformer", "demucs", "basic", "transkun", "mt3", "piano", "whisper"}
    results = []
    for model, spec in catalog.items():
        if spec["family"] not in families: continue
        current = root / "models" / model / "current"; current.mkdir(parents=True)
        original = installed / "models" / model / "current"
        shutil.copy2(original / "receipt.json", current / "receipt.json")
        for item in original.iterdir():
            if item.is_dir(): (current / item.name).symlink_to(item, target_is_directory=True)
        source = project.parent / "qa/fixtures" / ("reference.wav" if spec["family"] == "whisper" else "real-piano.wav")
        output = root / "outputs" / model
        start = time.monotonic()
        print("Starting:", model, flush=True)
        try:
            with (root / (model + ".log")).open("w") as log:
                subprocess.run([str(installed / "envs" / spec["family"] / "bin/python"), str(scripts / "utility.py"),
                    "--root", str(root), "--model", model, "--source", str(source), "--output", str(output), "--language", "en"],
                    env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1", AURORA_KEEP_TEST_FILES="1", HF_HUB_OFFLINE="1", OMP_NUM_THREADS="4"),
                    stdout=log, stderr=subprocess.STDOUT, check=True, timeout=600)
            artifacts = []
            for path in output.rglob("*"):
                if path.suffix == ".wav":
                    audio, rate = sf.read(path, dtype="float32")
                    assert len(audio) > 0 and np.isfinite(audio).all()
                    artifacts.append(dict(path=str(path), frames=len(audio), rate=rate))
                elif path.suffix == ".mid":
                    midi = pretty_midi.PrettyMIDI(str(path))
                    notes = sum(len(track.notes) for track in midi.instruments)
                    assert notes > 0
                    artifacts.append(dict(path=str(path), notes=notes))
                elif path.suffix == ".srt":
                    assert "-->" in path.read_text()
                    assert json.loads(path.with_suffix(".json").read_text())["segments"]
                    artifacts.append(dict(path=str(path), cues=path.read_text().count("-->")))
            # Upstream writes six stems plus the derived instrumental mix.
            expected = 2 if model == "roformer-vocals" else 7 if model == "roformer" else 4 if model == "demucs" else 1
            assert len(artifacts) == expected, f"Expected {expected} outputs, got {len(artifacts)}"
            if model == "roformer":
                assert {Path(item["path"]).stem.removeprefix("real-piano_") for item in artifacts} == {"vocals", "bass", "drums", "guitar", "piano", "other", "instrumental"}
            results.append(dict(model=model, ok=True, seconds=round(time.monotonic()-start, 2), artifacts=artifacts))
        except Exception as error:
            results.append(dict(model=model, ok=False, seconds=round(time.monotonic()-start, 2), error=str(error)))
        (root / "results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2))
        print(json.dumps(results[-1], ensure_ascii=False), flush=True)
    print("Acceptance files retained:", root, flush=True)
    return 0 if all(row["ok"] for row in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
