"""Explicit opt-in real inference acceptance; creates isolated metadata and keeps outputs."""
import json
import argparse
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import time
import urllib.request
import uuid

from gradio_client import Client, handle_file
import numpy as np
import soundfile as sf


def main():
    parser = argparse.ArgumentParser()
    qwen_models = ["qwen3-tts-06b-custom", "qwen3-tts-06b-base", "qwen3-tts-base", "qwen3-tts-custom", "qwen3-tts-design"]
    parser.add_argument("--models", nargs="+", choices=qwen_models + ["f5-tts", "seed-vc", "ace-step"], default=["qwen3-tts-06b-custom", "f5-tts"])
    selected = parser.parse_args().models
    scripts = Path(__file__).resolve().parent
    project = scripts.parents[3]
    source = Path.home() / "Aurora" / "Models"
    fixture = project.parent / "qa/fixtures/reference.wav"
    destination = project / ".local-check" / ("final-receipt-live-" + uuid.uuid4().hex)
    destination.mkdir(parents=True)
    rows = []
    for model, family in [(model, "qwen") for model in qwen_models] + [("f5-tts", "f5"), ("seed-vc", "seed"), ("ace-step", "ace")]:
        if model not in selected: continue
        current = destination / "models" / model / "current"
        current.mkdir(parents=True)
        shutil.copy2(source / "models" / model / "current/receipt.json", current / "receipt.json")
        for child in (source / "models" / model / "current").iterdir():
            if child.is_dir(): (current / child.name).symlink_to(child, target_is_directory=True)
        if family == "seed":
            shutil.copytree(source / "sources/seed-vc", destination / "sources/seed-vc", ignore=shutil.ignore_patterns(".git", "__pycache__"))
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0)); port = listener.getsockname()[1]
        receipts = destination / "receipts" / model
        environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTORCH_ENABLE_MPS_FALLBACK="1", AURORA_KEEP_TEST_FILES="1",
                           AURORA_RESULT_RECEIPTS=str(receipts), AURORA_MODEL_VERSION="isolated-acceptance", AURORA_RUNTIME_SIGNATURE="isolated-acceptance",
                           PYTHONPATH=str(project / "work/audio-studio/AuroraAudioStudio/Tools"))
        with (destination / (model + ".log")).open("w") as log:
            process = subprocess.Popen([str(source / "envs" / family / "bin/python"), "-u", str(scripts / "workbench.py"),
                "--root", str(destination), "--model", model, "--output", str(destination / "outputs"), "--port", str(port)],
                env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                deadline = time.monotonic() + 300
                while True:
                    if process.poll() is not None: raise RuntimeError("工作台启动失败，见日志")
                    try:
                        with urllib.request.urlopen(f"http://127.0.0.1:{port}/config", timeout=1): break
                    except (OSError, urllib.error.URLError):
                        if time.monotonic() > deadline: raise TimeoutError("工作台启动超时")
                        time.sleep(1)
                client = Client(f"http://127.0.0.1:{port}", download_files=False)
                if family == "qwen":
                    if model.endswith("-custom"):
                        job = client.submit("欢迎来到极光工作台。", "Chinese", "Vivian", "", api_name="/run_instruct")
                    elif model.endswith("-design"):
                        job = client.submit("欢迎来到极光工作台。", "Chinese", "温柔清晰的成年女声，语速平稳。", api_name="/run_voice_design")
                    else:
                        job = client.submit(handle_file(str(fixture)),
                            "Hello. This is an audio test for Aurora. Today the weather is clear, and we are testing local speech generation on this computer.",
                            False, "Welcome to Aurora.", "English", api_name="/run_voice_clone")
                    result = job.result(timeout=300)
                    final_path = result[0]
                elif family == "f5":
                    result = client.submit(handle_file(str(fixture)),
                        "Hello. This is an audio test for Aurora. Today the weather is clear, and we are testing local speech generation on this computer.",
                        "Welcome to Aurora. This is the final trimmed result.", True, False, 42, 0.15, 16, 1.0, api_name="/basic_tts").result(timeout=300)
                    final_path = result[0]
                elif family == "seed":
                    result = client.submit(handle_file(str(fixture)), handle_file(str(fixture)), 10, 1.0, 0.7, True, 0, api_name="/predict").result(timeout=300)
                    final_path = result[1]
                else:
                    api = client.view_api(return_format="dict", print_info=False)["named_endpoints"]["/generation_wrapper"]
                    parameters = {p["parameter_name"]: p.get("parameter_default") for p in api["parameters"]}
                    parameters.update(param_0="Gentle solo piano, simple melody, clean studio recording.", param_1="[Instrumental]", param_2=80,
                        param_5="unknown", param_8=False, param_9="42", param_11=6, param_12=1, param_36="wav", param_40=False)
                    result = client.submit(**parameters, api_name="/generation_wrapper").result(timeout=300)
                    final_path = next(item["path"] for item in result[8] if isinstance(item, dict) and str(item.get("path", "")).endswith(".wav"))
                if isinstance(final_path, dict): final_path = final_path["path"]
                recorded = list(receipts.glob("*.json"))
                if len(recorded) != 1: raise AssertionError(f"期待 1 个最终回执，实际 {len(recorded)}")
                receipt = json.loads(recorded[0].read_text())
                original, rate = sf.read(final_path, dtype="float32")
                imported, imported_rate = sf.read(receipt["path"], dtype="float32")
                assert len(original) > 0 and original.shape == imported.shape and rate == imported_rate
                assert np.max(np.abs(original - imported)) <= 1.0 / 32768 + 1e-7
                assert receipt["modelVersion"] == "isolated-acceptance" and receipt["device"] == ("mps+mlx" if family == "ace" else "mps")
                rows.append(dict(model=model, ok=True, frames=len(original), rate=rate, receipt=receipt["path"], final=final_path))
                print(json.dumps(rows[-1], ensure_ascii=False), flush=True)
                if family == "f5":
                    previous = set(receipts.glob("*.json"))
                    reference_text = "Hello. This is an audio test for Aurora. Today the weather is clear, and we are testing local speech generation on this computer."
                    names = ["Regular", "Second"] + [""] * 98
                    audios = [handle_file(str(fixture)), handle_file(str(fixture))] + [None] * 98
                    texts = [reference_text, reference_text] + [""] * 98
                    multi = client.submit('{"name":"Regular","seed":42,"speed":1.0} Welcome to Aurora. {"name":"Second","seed":43,"speed":1.0} This is the second segment.',
                        *names, *audios, *texts, True, api_name="/generate_multistyle_speech").result(timeout=300)
                    combined = multi[0]["path"] if isinstance(multi[0], dict) else multi[0]
                    new_receipts = set(receipts.glob("*.json")) - previous
                    assert len(new_receipts) == 1, "多段拼接只应收录一个最终结果"
                    combined_receipt = json.loads(new_receipts.pop().read_text())
                    final_audio, final_rate = sf.read(combined, dtype="float32")
                    saved_audio, saved_rate = sf.read(combined_receipt["path"], dtype="float32")
                    assert final_audio.shape == saved_audio.shape and final_rate == saved_rate and len(final_audio) > 0
                    assert np.max(np.abs(final_audio - saved_audio)) <= 1.0 / 32768 + 1e-7
                    rows.append(dict(model="f5-multistyle", ok=True, frames=len(final_audio), rate=final_rate, receipt=combined_receipt["path"], final=combined))
                    print(json.dumps(rows[-1], ensure_ascii=False), flush=True)
            except Exception as error:
                rows.append(dict(model=model, ok=False, error=str(error)))
                print(json.dumps(rows[-1], ensure_ascii=False), flush=True)
            finally:
                if process.poll() is None: os.killpg(process.pid, signal.SIGTERM)
                try: process.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL); process.wait()
    (destination / "results.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2))
    print("Acceptance files retained:", destination)
    return 0 if all(row["ok"] for row in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
