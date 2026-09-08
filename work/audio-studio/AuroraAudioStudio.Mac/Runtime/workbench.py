"""Embed each upstream Gradio workbench while owning its lifetime and results."""
import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import uuid

p = argparse.ArgumentParser()
p.add_argument("--root", type=Path, required=True)
p.add_argument("--model", required=True)
p.add_argument("--output", type=Path, required=True)
p.add_argument("--port", type=int, required=True)
p.add_argument("--language", default="zh-CN")
a = p.parse_args()
model = a.root / "models" / a.model / "current"
family = json.loads(Path(__file__).with_name("catalog.json").read_text())[a.model]["family"]
feature = {"ace":"music", "qwen":"voice", "seed":"singing", "f5":"voice"}[family]
session = a.root / "workspaces" / a.model
session.mkdir(parents=True, exist_ok=True)
output = a.output / {"music": "AI音乐", "voice": "AI配音", "singing": "AI歌声克隆"}[feature]
output.mkdir(parents=True, exist_ok=True)
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["GRADIO_ANALYTICS_ENABLED"] = "False"
os.environ["GRADIO_SERVER_NAME"] = "127.0.0.1"
os.environ["GRADIO_SERVER_PORT"] = str(a.port)
os.environ["GRADIO_TEMP_DIR"] = str(session / "temporary")
os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["AURORA_OUTPUT_ROOT"] = str(output)
os.environ["AURORA_FEATURE"] = feature
os.environ["AURORA_MODEL_ID"] = a.model
if not os.environ.get("AURORA_RESULT_RECEIPTS"):
    raise RuntimeError("缺少成果回执目录，请从 Aurora 启动工作台。")
import torch
os.environ["AURORA_DEVICE"] = "mps+mlx" if family == "ace" else "mps" if torch.backends.mps.is_available() else "cpu"
from gradio_result_bridge import install_bridge
install_bridge()


if family == "qwen":
    import torch
    from qwen_tts.cli.demo import main
    main([str(model / "weights"), "--device", "mps" if torch.backends.mps.is_available() else "cpu",
          "--dtype", "float32", "--no-flash-attn", "--concurrency", "1", "--ip", "127.0.0.1", "--port", str(a.port)])
elif family == "f5":
    os.chdir(session)
    import torch
    import torchaudio
    import soundfile as sf
    def read_audio(path, *args, **kwargs):
        # F5 preprocesses references to WAV. Read these with libsndfile so
        # inference does not depend on TorchCodec's FFmpeg dylib version.
        wave, rate = sf.read(path, dtype="float32", always_2d=True)
        return torch.from_numpy(wave.T.copy()), rate
    torchaudio.load = read_audio
    import cached_path
    import f5_tts.infer.utils_infer as utils
    upstream_cached_path = cached_path.cached_path
    def local_cached_path(url, *args, **kwargs):
        prefix = "hf://SWivid/F5-TTS/"
        if str(url).startswith(prefix):
            return model / "weights" / str(url)[len(prefix):]
        return upstream_cached_path(url, *args, **kwargs)
    cached_path.cached_path = local_cached_path
    upstream_vocoder = utils.load_vocoder
    utils.load_vocoder = lambda *args, **kwargs: upstream_vocoder(is_local=True, local_path=str(model / "vocos"))
    def transcribe_reference(ref_audio, language=None):
        from faster_whisper import WhisperModel
        asr_path = a.root / "models/whisper-large-v3-turbo/current/weights"
        if not asr_path.exists():
            raise RuntimeError("请填写参考音频文字，或在模型中心安装 Whisper Large v3 Turbo 以自动识别。")
        engine = WhisperModel(str(asr_path), device="cpu", compute_type="int8", local_files_only=True)
        segments, _ = engine.transcribe(str(ref_audio), language=language)
        return " ".join(s.text.strip() for s in segments)
    utils.transcribe = transcribe_reference
    from f5_tts.infer import infer_gradio
    infer_gradio.main(["--host", "127.0.0.1", "--port", str(a.port)])
elif family == "seed":
    source = a.root / "sources/seed-vc"
    sys.path.insert(0, str(source))
    os.chdir(source)
    import torch
    import yaml
    import app_svc
    import torch.nn.functional as functional
    def mps_convolution_fallback(original):
        def convolution(input, weight, bias=None, *args, **kwargs):
            try:
                return original(input, weight, bias, *args, **kwargs)
            except NotImplementedError:
                if input.device.type != "mps":
                    raise
                # BigVGAN's up/downsamplers exceed MPS convolution limits.
                # Run only unsupported operations on CPU, preserving their inputs.
                return original(input.cpu(), weight.cpu(), None if bias is None else bias.cpu(), *args, **kwargs).to(input.device)
        return convolution
    for name in ("conv1d", "conv_transpose1d"):
        setattr(functional, name, mps_convolution_fallback(getattr(functional, name)))
    # Resolve the exact pre-downloaded auxiliary weights, retaining upstream UI.
    def local_hf(repo_id, filename, config_filename=None):
        lookup = {"funasr/campplus": model / "campplus", "lj1995/VoiceConversionWebUI": model / "rmvpe", "Plachta/Seed-VC": model / "weights"}
        path = lookup[repo_id] / filename
        return (str(path), str(lookup[repo_id] / config_filename)) if config_filename else str(path)
    app_svc.load_custom_model_from_hf = local_hf
    config = yaml.safe_load((model / "weights/config_dit_mel_seed_uvit_whisper_base_f0_44k.yml").read_text())
    config["model_params"]["vocoder"]["name"] = str(model / "bigvgan")
    config["model_params"]["speech_tokenizer"]["name"] = str(model / "whisper")
    config_path = session / "local-config.yml"
    config_path.write_text(yaml.safe_dump(config))
    app_svc.device = torch.device("mps" if torch.backends.mps.is_available() else "cpu")
    app_svc.main(argparse.Namespace(checkpoint=str(model / "weights/DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema_v2.pth"), config=str(config_path), fp16=False, share=False))
elif family == "ace":
    checkpoints = session / "checkpoints"
    if checkpoints.is_symlink():
        checkpoints.unlink()
    checkpoints.mkdir(exist_ok=True)
    for source in (model / "checkpoints").rglob("*"):
        relative = source.relative_to(model / "checkpoints")
        if ".cache" in relative.parts or "__pycache__" in relative.parts:
            continue
        target = checkpoints / relative
        if source.is_dir():
            target.mkdir(parents=True, exist_ok=True)
        elif source.is_file():
            if target.is_symlink():
                if source.suffix != ".py" and target.resolve() == source.resolve():
                    continue
                retained = session / "replaced-links"
                retained.mkdir(exist_ok=True)
                target.rename(retained / (target.name + "-" + uuid.uuid4().hex))
            if source.suffix == ".py":
                shutil.copy2(source, target)
            elif not target.exists():
                target.symlink_to(source)
    gradio_outputs = session / "gradio_outputs"
    if gradio_outputs.is_symlink():
        retained = session / "replaced-links"
        retained.mkdir(exist_ok=True)
        gradio_outputs.rename(retained / ("gradio_outputs-" + uuid.uuid4().hex))
    gradio_outputs.mkdir(exist_ok=True)
    os.environ["ACESTEP_PROJECT_ROOT"] = str(session)
    os.environ["ACESTEP_CHECKPOINTS_DIR"] = str(checkpoints)
    os.environ["ACESTEP_LM_BACKEND"] = "mlx"
    os.chdir(session)
    import acestep.inference as inference
    import torch
    from acestep.core.generation.handler.init_service_downloads import InitServiceDownloadsMixin
    from acestep.core.generation.handler.init_service_loader import InitServiceLoaderMixin
    from acestep.core.generation.handler.mlx_dit_init import MlxDitInitMixin
    from acestep.core.generation.handler.mlx_vae_init import MlxVaeInitMixin
    def verify_models(self, *, checkpoint_path, config_path, **kwargs):
        for name in (config_path, "vae", "Qwen3-Embedding-0.6B", "acestep-5Hz-lm-1.7B"):
            if not (checkpoint_path / name / "config.json").is_file():
                raise RuntimeError("模型组件缺失，请在 Aurora 模型中心修复：" + name)
        return None
    InitServiceDownloadsMixin._ensure_models_present = verify_models
    original_loader = InitServiceLoaderMixin._load_main_model_from_checkpoint
    def load_mps(self, **kwargs):
        if kwargs.get("device") == "mps": self.dtype = torch.bfloat16
        return original_loader(self, **kwargs)
    InitServiceLoaderMixin._load_main_model_from_checkpoint = load_mps
    # Avoid keeping duplicate XL weights in both MLX and PyTorch on 24 GB Macs.
    # The LM uses MLX; the diffusion model uses MPS in its native BF16 precision.
    MlxDitInitMixin._init_mlx_dit = lambda self, **kwargs: False
    MlxVaeInitMixin._init_mlx_vae = lambda self, **kwargs: False
    sys.argv = ["acestep", "--port", str(a.port), "--server-name", "127.0.0.1",
        "--language", {"zh-CN":"zh","zh-TW":"zh","ja-JP":"ja"}.get(a.language,"en"),
        "--config_path", "acestep-v15-xl-turbo", "--lm_model_path", "acestep-5Hz-lm-1.7B",
        "--backend", "mlx", "--device", "mps", "--offload_to_cpu", "false", "--offload_dit_to_cpu", "false", "--init_service", "true", "--batch_size", "1"]
    import acestep.acestep_v15_pipeline as pipeline
    from acestep.ui.gradio.events.results import generation_info, generation_progress
    generation_info.DEFAULT_RESULTS_DIR = str(session / "gradio_outputs")
    generation_progress.DEFAULT_RESULTS_DIR = str(session / "gradio_outputs")
    # Upstream derives its output directory and LM checkpoint root from __file__.
    # Point that derivation at this model's writable workspace, not site-packages.
    pipeline.__file__ = str(session / "acestep/acestep_v15_pipeline.py")
    pipeline.main()
