"""No browser, server, model loading or inference. Test the Gradio config adapter."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import types
import unittest
from unittest.mock import patch

bridge_path = Path(__file__).resolve().parents[2] / "AuroraAudioStudio/Tools/gradio_result_bridge.py"
spec = importlib.util.spec_from_file_location("readiness_bridge", bridge_path)
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class IdentityTests(unittest.TestCase):
    def test_preserves_upstream_config_and_binds_this_launch(self):
        class Blocks:
            def get_config_file(self):
                return {"components": [{"id": 1}], "dependencies": [], "version": "fixture"}
        with patch.dict(os.environ, {"AURORA_WORKBENCH_INSTANCE": "this-launch"}):
            bridge.install_identity_bridge(types.SimpleNamespace(Blocks=Blocks))
            result = Blocks().get_config_file()
        self.assertEqual(result["aurora_instance"], "this-launch")
        self.assertEqual(result["components"], [{"id": 1}])
        self.assertEqual(result["version"], "fixture")

    def test_no_identity_hook_for_callers_without_launch_contract(self):
        with patch.dict(os.environ, {}, clear=True):
            bridge.install_identity_bridge(types.SimpleNamespace())

    def test_gradio6_missing_ids_and_upstream_explicit_ids(self):
        button = types.SimpleNamespace(_id=13, elem_id=None, get_block_name=lambda: "button")
        audio = types.SimpleNamespace(_id=16, elem_id="upstream-audio", get_block_name=lambda: "audio")
        class Blocks:
            blocks = {13: button, 16: audio}
            def get_config_file(self):
                return {}
        with patch.dict(os.environ, {"AURORA_WORKBENCH_INSTANCE": "this-launch"}):
            bridge.install_identity_bridge(types.SimpleNamespace(Blocks=Blocks))
            Blocks().get_config_file()
        self.assertEqual(button.elem_id, "aurora-component-13")
        self.assertEqual(audio.elem_id, "upstream-audio")


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--gradio-config":
        os.environ["GRADIO_ANALYTICS_ENABLED"] = "False"
        os.environ["AURORA_WORKBENCH_INSTANCE"] = "this-launch"
        import gradio as gr
        bridge.install_identity_bridge(gr)
        with gr.Blocks() as demo:
            text = gr.Textbox()
            action = gr.Button("Generate")
            output = gr.Audio(label="Generated Audio")
            action.click(lambda value: None, text, output)
        from fastapi.testclient import TestClient
        with TestClient(demo.app) as client:
            response = client.get("/config")
            assert response.status_code == 200
            config = response.json()
        assert config["aurora_instance"] == "this-launch"
        Path(sys.argv[2]).write_text(json.dumps(config), encoding="utf-8")
        print("PASS real Gradio /config route adapter:", gr.__version__, "(in-process client; no listening server or inference)")
    else:
        unittest.main()
