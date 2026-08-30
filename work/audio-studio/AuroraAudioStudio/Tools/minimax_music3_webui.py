import argparse
from datetime import datetime
from pathlib import Path

import gradio as gr
import soundfile as sf
import torch
from diffusers import ComponentsManager, ModularPipeline
from diffusers.hooks import apply_group_offloading


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--port", type=int, default=7860)
    parser.add_argument("--language", default="zh")
    return parser.parse_args()


args = parse_args()
output_root = Path(args.output)
output_root.mkdir(parents=True, exist_ok=True)

manager = ComponentsManager()
manager.enable_auto_cpu_offload(device="cuda")
pipe = ModularPipeline.from_pretrained(args.model, components_manager=manager)
pipe.load_components(dtype=torch.bfloat16)
apply_group_offloading(
    pipe.language_model,
    onload_device=torch.device("cuda"),
    offload_type="leaf_level",
    use_stream=True,
)


def generate(prompt, lyrics, duration, seed):
    if not prompt.strip():
        raise gr.Error("请填写音乐描述。")
    if not lyrics.strip():
        lyrics = "[Instrumental]"
    audio = pipe(
        prompt=prompt.strip(),
        lyrics=lyrics.strip(),
        audio_duration=float(duration),
        generator=torch.Generator("cuda").manual_seed(int(seed)),
        output="audios",
    )[0]
    destination = output_root / f"MiniMax-Music3-{datetime.now():%Y%m%d-%H%M%S}.wav"
    sf.write(destination, audio.T.float().cpu().numpy(), pipe.sampling_rate)
    return str(destination)


with gr.Blocks(title="MiniMax-Music3 · Aurora Audio Studio") as demo:
    gr.Markdown("# MiniMax-Music3\n由 Aurora 在本机运行；生成过程不会上传你的歌词或成品。")
    prompt = gr.Textbox(label="音乐描述", lines=5, placeholder="曲风、情绪、速度、调性、演唱与编曲……")
    lyrics = gr.Textbox(label="歌词 / 段落标签", lines=12, placeholder="[Verse]\n……\n[Chorus]\n……；纯音乐可填 [Instrumental]")
    with gr.Row():
        duration = gr.Slider(10, 300, value=60, step=1, label="时长（秒）")
        seed = gr.Number(value=7, precision=0, label="随机种子")
    button = gr.Button("生成音乐", variant="primary")
    audio = gr.Audio(label="生成结果", type="filepath")
    button.click(generate, [prompt, lyrics, duration, seed], audio)
    gr.Markdown("模型：MiniMax-Music3 · 许可：MiniMax-Music3 Community License · 仅支持 CUDA，当前为非流式生成。")

demo.queue(default_concurrency_limit=1).launch(server_name="127.0.0.1", server_port=args.port, share=False)
