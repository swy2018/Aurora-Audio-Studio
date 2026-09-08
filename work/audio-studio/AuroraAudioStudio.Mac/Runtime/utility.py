"""Real processing backends used by the native Mac utility workspaces."""
import argparse
import contextlib
import json
import math
import os
from pathlib import Path
import subprocess
import shutil
import sys
import tempfile
import time


def event(kind, **kw):
    print(json.dumps(dict(aurora=kind, **kw), ensure_ascii=False), flush=True)


def timestamp(seconds):
    ms = round(max(0, seconds) * 1000)
    h, ms = divmod(ms, 3600000)
    m, ms = divmod(ms, 60000)
    s, ms = divmod(ms, 1000)
    return f"{h:02}:{m:02}:{s:02},{ms:03}"


def prepare_audio(model, source, temporary):
    if model not in ("roformer", "roformer-vocals", "piano"):
        return source
    prepared = temporary / (source.stem + ".wav")
    if source.suffix.lower() == ".wav":
        shutil.copy2(source, prepared)
    else:
        subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-i", str(source),
                        "-vn", "-ar", "44100", "-ac", "2", str(prepared)], check=True)
    return prepared


def save_subtitles(output, source, language, rows):
    target = output / (source.stem + ".srt")
    text = "\n\n".join(f"{i}\n{timestamp(s['start'])} --> {timestamp(s['end'])}\n{s['text']}" for i, s in enumerate(rows, 1))
    target.write_text(text + ("\n" if rows else ""), encoding="utf-8")
    target.with_suffix(".json").write_text(json.dumps(dict(language=language, segments=rows), ensure_ascii=False, indent=2), encoding="utf-8")


def execute(root, model, source, output, language):
    current = root / "models" / model / "current"
    if not (current / "receipt.json").exists():
        raise RuntimeError("模型尚未完整安装，请先在模型管理中安装或修复。")
    output.mkdir(parents=True, exist_ok=False)
    message = "处理完成"
    midi_output = output / (source.stem + time.strftime("-%Y%m%d-%H%M%S.mid"))
    event("progress", stage="正在读取素材", progress=0.05)
    temporary = contextlib.nullcontext(tempfile.mkdtemp(prefix="aurora-audio-test-")) if os.environ.get("AURORA_KEEP_TEST_FILES") == "1" else tempfile.TemporaryDirectory(prefix="aurora-audio-")
    with temporary as tmp:
        tmp = Path(tmp)
        audio = prepare_audio(model, source, tmp)
        event("progress", stage="正在加载模型并处理", progress=0.1)
        if model.startswith("whisper-"):
            from faster_whisper import WhisperModel
            engine = WhisperModel(str(current / "weights"), device="cpu", compute_type="int8", cpu_threads=min(os.cpu_count() or 4, 8), local_files_only=True)
            segments, info = engine.transcribe(str(audio), language=None if language == "auto" else language,
                vad_filter=True, beam_size=5, word_timestamps=True)
            rows = []
            for s in segments:
                rows.append(dict(start=s.start, end=s.end, text=s.text.strip(), words=[dict(start=w.start,end=w.end,word=w.word) for w in (s.words or [])]))
                event("progress", stage="正在识别字幕", progress=min(.95, .1 + .85 * s.end / max(info.duration, 1)))
            save_subtitles(output, source, info.language, rows)
            if not rows:
                message = "未检测到语音，已保存空字幕和识别记录。"
        elif model in ("roformer", "roformer-vocals"):
            import torch
            from bs_roformer import get_model_from_config
            from bs_roformer.inference import SafeLoaderWithTuple
            import yaml
            # Invoke the package CLI so its overlap/add normalization is preserved.
            weights = current / "weights"
            config = next(weights.glob("*.yaml"))
            checkpoint = next(weights.glob("*.ckpt"))
            subprocess.run([str(Path(sys.executable).with_name("bs-roformer-infer")),
                "--input_folder", str(tmp), "--store_dir", str(output),
                "--config_path", str(config), "--model_path", str(checkpoint), "--device", "cpu"], check=True)
        elif model == "transkun":
            import torch
            from transkun import transcribe
            original_load = torch.load
            def load(*a, **kw):
                kw.setdefault("weights_only", False)
                return original_load(*a, **kw)
            torch.load = load
            sys.argv = ["transkun", str(audio), str(midi_output), "--device", "cpu", "--weight", str(current / "bundled/2.0.pt"), "--conf", str(current / "bundled/2.0.conf")]
            transcribe.main()
        elif model == "basic-pitch":
            from basic_pitch.inference import predict
            _, midi, _ = predict(str(audio), str(current / "bundled/nmp.onnx"))
            midi.write(str(output / (source.stem + "_basic_pitch.mid")))
        elif model == "yourmt3":
            import librosa
            from mt3_infer import transcribe
            wave, sr = librosa.load(str(audio), sr=16000)
            midi = transcribe(wave, model="yourmt3", sr=sr, checkpoint_path=str(next((current / "weights").rglob("last.ckpt"))), device="cpu", auto_download=False)
            midi.save(str(midi_output))
        elif model == "piano":
            import numpy as np
            import soundfile as sf
            from scipy.signal import resample_poly
            import torch
            from piano_transcription_inference import PianoTranscription, sample_rate
            original_load = torch.load
            def load(*args, **kwargs):
                kwargs.setdefault("weights_only", False)
                return original_load(*args, **kwargs)
            torch.load = load
            wave, sr = sf.read(str(audio), dtype="float32")
            wave = wave.mean(axis=1) if wave.ndim > 1 else wave
            divisor = math.gcd(sr, sample_rate)
            if sr != sample_rate:
                wave = resample_poly(wave, sample_rate // divisor, sr // divisor).astype(np.float32)
            engine = PianoTranscription(checkpoint_path=str(current / "weights/piano.pth"), device="cpu")
            engine.transcribe(wave, str(midi_output))
        elif model == "demucs":
            import demucs.pretrained
            from demucs.hf import load_safetensors_model
            from demucs.apply import BagOfModels
            import yaml
            weights = current / "weights"
            bag = yaml.safe_load((weights / "htdemucs.yaml").read_text())
            engine = BagOfModels([load_safetensors_model(weights / (sig + ".safetensors")) for sig in bag["models"]], bag.get("weights"), bag.get("segment"))
            demucs.pretrained.get_model = lambda *args, **kwargs: engine
            from demucs.separate import main
            sys.argv = ["demucs", "-n", "htdemucs", "-d", "cpu", "-o", str(output), str(audio)]
            main()
        else:
            raise ValueError("暂未接入此模型的 Mac 处理引擎。")
    outputs = [str(p) for p in sorted(output.rglob("*")) if p.is_file() and p.suffix.lower() in (".wav", ".mid", ".srt")]
    if not outputs:
        raise RuntimeError("处理程序退出，但没有生成有效结果。")
    event("result", outputs=outputs, device="CPU", message=message)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path, required=True)
    p.add_argument("--model", required=True)
    p.add_argument("--source", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--language", default="auto")
    a = p.parse_args()
    execute(a.root, a.model, a.source, a.output, a.language)
