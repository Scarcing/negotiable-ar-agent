"""End-to-end check on a synthetic tabletop image; not a Quest accuracy benchmark."""
import base64
import json
import os
from pathlib import Path
import sys
import threading
import time
from urllib.request import Request, urlopen

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "server"))
import service
image = Path(__file__).resolve().parents[1] / "server" / "warmup.jpg"
body = {"frameId": "synthetic-calibration", "imageBase64": base64.b64encode(image.read_bytes()).decode(),
        "task": {"name": "Unselected creative task", "goal": "Explore arrangements of colored geometric pieces", "constraints": "There is no single correct arrangement"},
        "hands": [], "depthAvailable": False}

# Cold GPU/model loading is deliberately outside the interactive request deadline.
with urlopen(Request(service.VLM_URL + "/api/generate", json.dumps({"model": service.MODEL, "keep_alive": "15m", "stream": False}).encode(), {"Content-Type": "application/json"}), timeout=120) as response:
    json.load(response)
bridge = service.ThreadingHTTPServer(("127.0.0.1", 0), service.Handler)
thread = threading.Thread(target=bridge.serve_forever, daemon=True); thread.start()
url = "http://127.0.0.1:" + str(bridge.server_port)
headers = {"Content-Type": "application/json"}
if service.TOKEN:
    headers["Authorization"] = "Bearer " + service.TOKEN
try:
    for sample in range(3):
        started = time.monotonic()
        with urlopen(Request(url + "/observe", json.dumps(body).encode(), headers), timeout=10) as response:
            result = json.load(response)
        assert result["frameId"] == body["frameId"] and len(result["items"]) > 0
        print(json.dumps({"sample": sample + 1, "wallMs": round((time.monotonic() - started) * 1000), "observation": result}), flush=True)
    with urlopen(Request(url + "/speak", json.dumps({"text": "Would you like a small hint, or time to explore?"}).encode(), headers), timeout=15) as response:
        wav = response.read()
    assert wav.startswith(b"RIFF") and len(wav) > 44
    print(json.dumps({"tts": "valid WAV over HTTP", "bytes": len(wav)}))
finally:
    bridge.shutdown(); bridge.server_close(); thread.join()
