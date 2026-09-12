"""Observe supported generation calls without changing their inputs, outputs or exceptions."""
import contextvars
import functools
import inspect
import json
import os
from pathlib import Path
import time
import uuid

active_task = contextvars.ContextVar("aurora_task", default=None)


def install(gr):
    directory = os.environ.get("AURORA_TASK_EVENTS")
    instance = os.environ.get("AURORA_WORKBENCH_INSTANCE")
    if not directory or not instance:
        return
    original = gr.Blocks.process_api
    signature = inspect.signature(original)
    calls = {}
    supported = {"generation_wrapper", "run_voice_clone", "run_instruct", "run_voice_design", "predict", "infer", "generate"}

    def publish(task, status, message=""):
        try:
            task["sequence"] += 1
            value = {k: v for k, v in task.items() if k != "has_output"}
            value.update(status=status, message=message, at=time.time())
            root = Path(directory)
            root.mkdir(parents=True, exist_ok=True)
            pending = root / (task["id"] + ".tmp")
            pending.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
            pending.replace(root / (task["id"] + ".json"))
        except Exception as error:
            print("AURORA_TASK_EVENT_ERROR " + str(error), flush=True)

    @functools.wraps(original)
    async def process_api(blocks, *args, **kwargs):
        # If an upstream signature is not recognized, preserve its original behavior.
        try:
            arguments = signature.bind(blocks, *args, **kwargs).arguments
            fn = arguments.get("block_fn")
            if isinstance(fn, int):
                fn = blocks.fns[fn]
            event = arguments.get("event_id")
            observed = event and str(getattr(fn, "api_name", "")).lstrip("/") in supported
        except Exception:
            observed = False
        if not observed:
            return await original(blocks, *args, **kwargs)
        key = str(event)
        task = calls.setdefault(key, dict(id=uuid.uuid5(uuid.NAMESPACE_URL, instance + ":" + key).hex,
            feature=os.environ.get("AURORA_FEATURE", ""), modelId=os.environ.get("AURORA_MODEL_ID", ""),
            pid=os.getpid(), instance=instance, sequence=0, has_output=False))
        if task["sequence"] == 0:
            publish(task, "running")
        context = active_task.set(task)
        try:
            result = await original(blocks, *args, **kwargs)
            if not result.get("is_generating", False):
                publish(task, "saving" if task["has_output"] else "failed",
                        "" if task["has_output"] else "工作台已结束，但未检测到可收录的音频成品。")
                calls.pop(key, None)
            return result
        except BaseException:
            publish(task, "failed", "工作台生成未完成，请查看引擎日志。")
            calls.pop(key, None)
            raise
        finally:
            active_task.reset(context)

    gr.Blocks.process_api = process_api
