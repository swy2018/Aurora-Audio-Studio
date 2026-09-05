"""Run an upstream Gradio entry point and persist completed audio with an explicit receipt."""
import argparse
import importlib.metadata
import json
import os
from pathlib import Path
import runpy
import sys
import uuid


def is_result_audio(label, streaming=False):
    if streaming:
        return False
    label = (label or "").lower()
    return any(word in label for word in ("output audio", "generated", "synthesized", "生成音乐", "生成音樂", "生成的音乐", "生成的音樂", "生成音频", "生成結果", "生成结果", "合成结果", "完整输出"))


def install_bridge():
    import gradio as gr
    import soundfile as sf
    import torch
    original = gr.Audio.postprocess
    seen = set()
    if os.environ.get("AURORA_MODEL_ID") == "seed-vc":
        original_interface = gr.Interface.__init__

        def singing_interface(component, *args, **kwargs):
            # Replace the duplicated bilingual introduction, retaining both input limits.
            kwargs["description"] = "Reference audio is limited to 25 seconds. When source and reference exceed 30 seconds combined, the source is processed in chunks."
            original_interface(component, *args, **kwargs)

        gr.Interface.__init__ = singing_interface

    def postprocess(component, value):
        result = original(component, value)
        if result is None or not is_result_audio(component.label, getattr(component, "streaming", False)):
            return result
        path = result.get("path") if isinstance(result, dict) else getattr(result, "path", None)
        if not path or not Path(path).is_file():
            return result
        signature = (str(Path(path).resolve()), Path(path).stat().st_mtime_ns)
        if signature in seen:
            return result
        try:
            output = Path(os.environ["AURORA_OUTPUT_ROOT"])
            receipts = Path(os.environ["AURORA_RESULT_RECEIPTS"])
            identity = uuid.uuid4().hex
            directory = output / ("generated-" + identity)
            directory.mkdir(parents=True, exist_ok=False)
            audio, rate = sf.read(path, dtype="float32")
            if len(audio) == 0:
                raise ValueError("Generated audio is empty")
            destination = directory / "audio.wav"
            sf.write(destination, audio, rate, subtype="PCM_16")
            receipt = dict(id=identity, feature=os.environ["AURORA_FEATURE"], modelId=os.environ["AURORA_MODEL_ID"], path=str(destination.resolve()), device="cuda" if torch.cuda.is_available() else "cpu")
            receipts.mkdir(parents=True, exist_ok=True)
            pending = receipts / (identity + ".tmp")
            pending.write_text(json.dumps(receipt, ensure_ascii=False), encoding="utf-8")
            pending.replace(receipts / (identity + ".json"))
            seen.add(signature)
            print("AURORA_RESULT " + str(destination), flush=True)
        except Exception as error:
            # The upstream result remains downloadable even if Aurora's library is unavailable.
            print("AURORA_RESULT_ERROR " + str(error), file=sys.stderr, flush=True)
        return result

    gr.Audio.postprocess = postprocess


def main():
    parser = argparse.ArgumentParser()
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument("--script")
    target.add_argument("--console-script")
    args, remaining = parser.parse_known_args()
    if remaining and remaining[0] == "--":
        remaining.pop(0)
    install_bridge()
    if args.script:
        script = Path(args.script).resolve()
        sys.path.insert(0, str(script.parent))
        sys.argv = [str(script)] + remaining
        runpy.run_path(str(script), run_name="__main__")
    else:
        sys.argv = [args.console_script] + remaining
        entries = importlib.metadata.entry_points(group="console_scripts")
        entry = next(item for item in entries if item.name == args.console_script)
        entry.load()()


if __name__ == "__main__":
    main()
