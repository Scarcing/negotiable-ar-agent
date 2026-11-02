"""Load the local GPU and image encoder before starting interactive deadlines."""
import base64
import json
import os
from pathlib import Path
from urllib.request import Request, urlopen

url = os.getenv("VLM_URL", "http://127.0.0.1:11434").rstrip("/") + "/api/chat"
image = base64.b64encode(Path(__file__).with_name("warmup.jpg").read_bytes()).decode()
body = {"model": os.getenv("VLM_MODEL", "qwen3-vl:2b-instruct"), "stream": False, "keep_alive": "15m",
        "messages": [{"role": "user", "content": "Describe the visible shapes.", "images": [image]}],
        "options": {"num_ctx": 4096, "num_predict": 1}}
with urlopen(Request(url, json.dumps(body).encode(), {"Content-Type": "application/json"}), timeout=120) as response:
    json.load(response)
print("Local model and vision encoder warmed.")
