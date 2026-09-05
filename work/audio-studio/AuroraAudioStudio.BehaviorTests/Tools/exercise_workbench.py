"""Opt-in acceptance through the same Gradio callbacks used by the embedded UI."""
import json
from pathlib import Path
import sys
from gradio_client import Client, handle_file

feature, model, url, evidence, source = sys.argv[1:]
root = Path(evidence)
root.mkdir(parents=True, exist_ok=True)
client = Client(url, download_files=False)
api = client.view_api(return_format="dict", print_info=False)
(root / "api.json").write_text(json.dumps(api, ensure_ascii=False, indent=2), encoding="utf-8")
print("Endpoints:", list(api["named_endpoints"]), flush=True)
if feature == "voice":
    endpoint = "/run_instruct" if model.endswith("custom") else "/run_voice_design" if model.endswith("design") else "/run_voice_clone"
    print(json.dumps(api["named_endpoints"][endpoint], ensure_ascii=False), flush=True)
    if model.endswith("custom"):
        result = client.predict("Aurora audio studio. This is a short local voice test.", "English", "Ryan", "Calm and clear.", api_name=endpoint)
    elif model.endswith("design"):
        result = client.predict("Aurora audio studio. This is a voice design test.", "English", "A warm clear female narrator speaking naturally.", api_name=endpoint)
    else:
        result = client.predict(handle_file(source), "Aurora audio studio. This is a short local voice test.", False, "Aurora audio studio. The voice clone test is complete.", "English", api_name=endpoint)
elif feature == "singing":
    result = client.predict(handle_file(source), handle_file(source), 10, 1.0, 0.7, True, 0, api_name="/predict")
else:
    endpoint = "/generation_wrapper"
    parameters = api["named_endpoints"][endpoint]["parameters"]
    overrides = {"param_0": "Solo acoustic piano playing a clear gentle melody, distinct notes, clean recording, no drums, no vocals.", "param_1": "[Instrumental]", "param_2": 90, "param_3": "C major", "param_4": "4", "param_8": False, "param_9": "20260905", "param_11": 10, "param_12": 1, "param_36": "wav", "param_40": False, "param_45": False, "param_46": False, "param_47": False, "param_73": False}
    arguments = []
    for parameter in parameters:
        value = overrides.get(parameter["parameter_name"], parameter["parameter_default"])
        if value is None and parameter["component"] == "Textbox": value = ""
        arguments.append(value)
    result = client.predict(*arguments, api_name=endpoint)
(root / "client-result.json").write_text(json.dumps(result, ensure_ascii=False, default=str, indent=2), encoding="utf-8")
print("GRADIO_GENERATION_RETURNED", result, flush=True)
