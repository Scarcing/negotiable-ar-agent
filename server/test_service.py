import base64
from contextlib import contextmanager
import io
import json
import math
import threading
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import service


def observation():
    return {"summary": "Colored pieces on a table", "phase": "arranging", "visibleAction": "unknown",
            "suggestedHint": "What shape are you exploring?", "confidence": .8,
            "items": [{"label": "red triangle", "confidence": .7, "x": .9, "y": .8, "width": .4, "height": .4}]}


def frame():
    return {"frameId": "test-1", "task": {"goal": "Explore"}, "imageBase64": base64.b64encode(b"\xff\xd8\xfftest").decode(), "hands": []}


class ValidationTests(unittest.TestCase):
    def test_clips_boxes_to_image(self):
        box = service.clean_observation(observation())["items"][0]
        self.assertAlmostEqual(box["width"], .1)
        self.assertAlmostEqual(box["height"], .2)

    def test_rejects_bad_confidence(self):
        for value in [None, "high", True, float("nan"), float("inf"), -1, 1000]:
            data = observation(); data["confidence"] = value
            with self.assertRaises(service.BadObservation): service.clean_observation(data)

    def test_missing_schema(self):
        with self.assertRaises(service.BadObservation): service.clean_observation({"summary": "yes"})

    def test_invalid_boxes_preserve_labels_without_fabricating_locations(self):
        data = observation(); data["items"][0]["x"] = 700
        item = service.clean_observation(data)["items"][0]
        self.assertEqual(item["label"], "red triangle")
        self.assertFalse(item["boundsValid"])
        self.assertEqual(item["width"], 0)

    def test_rejects_non_jpeg(self):
        data = frame(); data["imageBase64"] = base64.b64encode(b"not an image").decode()
        with self.assertRaises(ValueError): service.validate_frame(data)

    def test_foreground_metadata_and_crop_fallback(self):
        data = frame()
        data["foreground"] = {"mode": "crop", "cropX": .12, "cropY": .04, "cropWidth": .76, "cropHeight": .72, "depthMasked": False}
        context = service.foreground_context(data)
        self.assertEqual(context["mode"], "crop")
        self.assertEqual(context["boxesRelativeTo"], "processed_image")
        self.assertNotIn("maxDistanceMetres", context)
        data["foreground"].update(mode="crop_depth", depthMasked=True, maxDistanceMetres=1.4)
        self.assertEqual(service.foreground_context(data)["maxDistanceMetres"], 1.4)

    def test_false_depth_mask_claim_is_rejected(self):
        data = frame(); data["foreground"] = {"mode": "crop_depth", "cropX": 0, "cropY": 0, "cropWidth": 1, "cropHeight": 1, "depthMasked": False}
        with self.assertRaises(ValueError): service.foreground_context(data)

    def test_invalid_foreground_crop_is_rejected(self):
        data = frame(); data["foreground"] = {"mode": "crop", "cropX": .8, "cropY": 0, "cropWidth": .8, "cropHeight": 1}
        with self.assertRaises(ValueError): service.foreground_context(data)

    def test_foreground_mode_reaches_the_model(self):
        data = frame(); data["foreground"] = {"mode": "crop", "cropX": 0, "cropY": 0, "cropWidth": 1, "cropHeight": .75}
        response = io.BytesIO(json.dumps({"message": {"content": json.dumps(observation())}}).encode())
        with patch("service.urlopen", return_value=response) as api:
            result = service.call_vlm(data)
        prompt = json.loads(api.call_args.args[0].data)["messages"][0]["content"]
        self.assertIn('"mode": "crop"', prompt)
        self.assertEqual(result["foregroundMode"], "crop")

    def test_ollama_contract(self):
        response = io.BytesIO(json.dumps({"message": {"content": json.dumps(observation())}}).encode())
        with patch("service.urlopen", return_value=response) as api:
            result = service.call_vlm(frame())
        self.assertEqual(result["frameId"], "test-1")
        payload = json.loads(api.call_args.args[0].data)
        self.assertFalse(payload["stream"])
        self.assertEqual(payload["format"], service.SCHEMA)
        self.assertEqual(payload["messages"][0]["images"][0], frame()["imageBase64"])

    def test_compatible_provider_contract(self):
        response = io.BytesIO(json.dumps({"choices": [{"message": {"content": json.dumps(observation())}}]}).encode())
        with patch("service.PROVIDER", "compatible"), patch("service.urlopen", return_value=response) as api:
            self.assertEqual(service.call_vlm(frame())["frameId"], "test-1")
        self.assertTrue(api.call_args.args[0].full_url.endswith("/chat/completions"))


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = service.ThreadingHTTPServer(("127.0.0.1", 0), service.Handler)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True); cls.thread.start()
        cls.url = "http://127.0.0.1:" + str(cls.server.server_port)

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown(); cls.server.server_close(); cls.thread.join()

    def post(self, data, path="/observe", headers=None):
        return urlopen(Request(self.url + path, json.dumps(data).encode(), headers or {"Content-Type": "application/json"}), timeout=3)

    def test_busy_discards_frame(self):
        service.INFERENCE.acquire()
        try:
            with self.assertRaises(HTTPError) as error: self.post(frame())
            self.assertEqual(error.exception.code, 429)
        finally: service.INFERENCE.release()

    def test_auth(self):
        with patch("service.TOKEN", "test-secret"):
            with self.assertRaises(HTTPError) as error: self.post(frame())
            self.assertEqual(error.exception.code, 401)

    def test_unavailable_returns_503(self):
        with patch("service.call_vlm", side_effect=TimeoutError):
            with self.assertRaises(HTTPError) as error: self.post(frame())
        self.assertEqual(error.exception.code, 503)

    def test_malformed_output_fails_closed(self):
        with patch("service.call_vlm", side_effect=service.BadObservation):
            with self.assertRaises(HTTPError) as error: self.post(frame())
        self.assertEqual(error.exception.code, 422)

    def test_observation_round_trip(self):
        with patch("service.call_vlm", return_value={**observation(), "frameId": "test-1"}):
            with self.post(frame()) as response: self.assertEqual(json.load(response)["frameId"], "test-1")

    def test_speech_round_trip(self):
        with patch("service.synthesize", return_value=b"RIFFtestWAVE"):
            with self.post({"text": "hello"}, "/speak") as response: self.assertEqual(response.headers["Content-Type"], "audio/wav")


if __name__ == "__main__": unittest.main()
