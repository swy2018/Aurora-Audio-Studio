from __future__ import annotations

import argparse
import gc
import hashlib
import json
from pathlib import Path

import soundfile as sf
import torch
from qwen_tts import Qwen3TTSModel


def save_audio(output: Path, name: str, wav, sample_rate: int) -> dict:
    path = output / name
    sf.write(path, wav, sample_rate)
    data, actual_rate = sf.read(path, always_2d=False)
    return {
        "file": str(path),
        "sample_rate": actual_rate,
        "duration_seconds": round(len(data) / actual_rate, 3),
        "peak": round(float(abs(data).max()), 6),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def load_model(path: Path) -> Qwen3TTSModel:
    return Qwen3TTSModel.from_pretrained(
        str(path),
        device_map="cuda:0",
        dtype=torch.bfloat16,
        attn_implementation="sdpa",
    )


def validate_custom(model_root: Path, output: Path) -> list[dict]:
    model = load_model(model_root / "Qwen3-TTS-12Hz-1.7B-CustomVoice")
    cases = [
        ("custom-zh-cn.wav", "欢迎使用 Aurora Audio Studio，这是一段简体中文语音测试。", "Chinese", "Vivian", "清晰、自然、专业"),
        ("custom-zh-tw.wav", "歡迎使用 Aurora Audio Studio，這是一段繁體中文語音測試。", "Chinese", "Serena", "溫和、自然、清晰"),
        ("custom-en.wav", "Welcome to Aurora Audio Studio. This is an English voice quality test.", "English", "Ryan", "Calm, clear, and professional."),
        ("custom-ja.wav", "Aurora Audio Studioへようこそ。これは日本語の音声テストです。", "Japanese", "Ono_Anna", "自然で明るく、聞き取りやすく話してください。"),
    ]
    results = []
    for filename, text, language, speaker, instruct in cases:
        wavs, sample_rate = model.generate_custom_voice(
            text=text,
            language=language,
            speaker=speaker,
            instruct=instruct,
        )
        results.append(save_audio(output, filename, wavs[0], sample_rate))
    return results


def validate_clone(model_root: Path, output: Path, reference: Path) -> list[dict]:
    model = load_model(model_root / "Qwen3-TTS-12Hz-1.7B-Base")
    wavs, sample_rate = model.generate_voice_clone(
        text="Aurora 已完成新的声音克隆模型验证。",
        language="Chinese",
        ref_audio=str(reference),
        ref_text="Okay. Yeah. I resent you. I love you. I respect you. But you know what? You blew it! And thanks to you.",
    )
    return [save_audio(output, "voice-clone-zh.wav", wavs[0], sample_rate)]


def validate_design(model_root: Path, output: Path) -> list[dict]:
    model = load_model(model_root / "Qwen3-TTS-12Hz-1.7B-VoiceDesign")
    wavs, sample_rate = model.generate_voice_design(
        text="欢迎来到 Aurora，你的本地声音创作工作台。",
        language="Chinese",
        instruct="成熟自然的女性声音，音色温暖，吐字清晰，像专业纪录片旁白。",
    )
    return [save_audio(output, "voice-design-zh.wav", wavs[0], sample_rate)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("custom", "clone", "design"))
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reference", type=Path)
    args = parser.parse_args()

    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available")
    args.output.mkdir(parents=True, exist_ok=True)
    if args.mode == "custom":
        results = validate_custom(args.model_root, args.output)
    elif args.mode == "clone":
        if args.reference is None or not args.reference.is_file():
            raise FileNotFoundError("A valid --reference file is required")
        results = validate_clone(args.model_root, args.output, args.reference)
    else:
        results = validate_design(args.model_root, args.output)

    report = {
        "mode": args.mode,
        "torch": torch.__version__,
        "cuda": torch.version.cuda,
        "gpu": torch.cuda.get_device_name(0),
        "results": results,
    }
    report_path = args.output / f"validation-{args.mode}.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    del results
    gc.collect()
    torch.cuda.empty_cache()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
