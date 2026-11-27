"""Isolated TF/ONNX acceptance on an existing short fixture; never installs packages."""
import argparse
import json
from pathlib import Path
import time


def predict(backend, source, output):
    import importlib.metadata
    import importlib.resources
    import importlib.util
    import numpy as np
    from basic_pitch.inference import predict as infer
    if backend == "onnx" and importlib.util.find_spec("tensorflow") is not None:
        raise RuntimeError("ONNX acceptance environment unexpectedly contains TensorFlow")
    model = importlib.resources.files("basic_pitch") / "saved_models/icassp_2022" / ("nmp.onnx" if backend == "onnx" else "nmp")
    output.mkdir(parents=True, exist_ok=False)
    start = time.monotonic()
    tensors, midi, _ = infer(str(source), str(model))
    notes = sorted([dict(pitch=int(n.pitch), start=float(n.start), end=float(n.end), velocity=int(n.velocity))
                    for instrument in midi.instruments for n in instrument.notes], key=lambda n: (n["start"], n["pitch"]))
    if not notes or not all(np.isfinite(tensor).all() for tensor in tensors.values()):
        raise RuntimeError("Invalid model output")
    midi.write(str(output / "result.mid"))
    np.savez_compressed(output / "tensors.npz", **tensors)
    result = dict(backend=backend, package=importlib.metadata.version("basic-pitch"), elapsed=time.monotonic()-start,
                  input=str(source.resolve()), notes=notes)
    (output / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(dict(backend=backend, notes=len(notes), output=str(output))))


def compare(left, right):
    import numpy as np
    a = json.loads((left / "result.json").read_text())
    b = json.loads((right / "result.json").read_text())
    if a["input"] != b["input"] or len(a["notes"]) != len(b["notes"]):
        raise RuntimeError("Input identity or note count differs")
    errors = []
    for x, y in zip(a["notes"], b["notes"]):
        if x["pitch"] != y["pitch"]:
            raise RuntimeError("Predicted pitch differs")
        errors += [abs(x["start"] - y["start"]), abs(x["end"] - y["end"])]
    if max(errors, default=0) > .03:
        raise RuntimeError("Note timing differs by more than 30 ms")
    differences = {}
    with np.load(left / "tensors.npz") as ta, np.load(right / "tensors.npz") as tb:
        for key in ta.files:
            if ta[key].shape != tb[key].shape:
                raise RuntimeError("Tensor shape mismatch: " + key)
            differences[key] = float(np.max(np.abs(ta[key] - tb[key])))
            if not np.allclose(ta[key], tb[key], atol=.005, rtol=.001):
                raise RuntimeError("Model tensor differs beyond acceptance tolerance: " + key)
    receipt = dict(passed=True, notes=len(a["notes"]), max_note_seconds=max(errors, default=0), tensor_max_abs=differences,
                   scope="One short fixture, not a claim of universal transcription quality or performance.")
    (right / "comparison.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["tf", "onnx", "compare"])
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if args.mode == "compare":
        compare(args.source, args.output)
    else:
        predict(args.mode, args.source, args.output)
