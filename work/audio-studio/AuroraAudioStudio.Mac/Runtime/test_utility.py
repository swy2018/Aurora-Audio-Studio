import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import wave

import utility


class UtilityParityTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="aurora-utility-parity-"))
        self.source = self.root / "钢琴 24bit.wav"
        with wave.open(str(self.source), "wb") as output:
            output.setnchannels(2)
            output.setsampwidth(3)
            output.setframerate(48000)
            output.writeframes(b"\x00\x01\x02\x03\x04\x05" * 48)
        self.prepared = self.root / "prepared"
        self.prepared.mkdir()

    def tearDown(self):
        print("Test files retained:", self.root)

    def test_direct_backends_receive_original_without_quantization(self):
        with patch.object(utility.subprocess, "run") as process:
            for model in ("demucs", "basic-pitch", "transkun", "yourmt3", "whisper-small"):
                self.assertEqual(utility.prepare_audio(model, self.source, self.prepared), self.source)
            process.assert_not_called()

    def test_roformer_and_piano_preserve_existing_wave_bytes(self):
        for model in ("roformer", "roformer-vocals", "piano"):
            with self.subTest(model=model), patch.object(utility.subprocess, "run") as process:
                result = utility.prepare_audio(model, self.source, self.prepared)
                self.assertEqual(result.name, self.source.name)
                self.assertEqual(result.read_bytes(), self.source.read_bytes())
                process.assert_not_called()

    def test_non_wave_preparation_matches_windows_rate_and_channels(self):
        source = self.root / "演奏.mp3"
        with patch.object(utility.subprocess, "run") as process:
            result = utility.prepare_audio("roformer", source, self.prepared)
        args = process.call_args.args[0]
        self.assertEqual(args[args.index("-ar") + 1], "44100")
        self.assertEqual(args[args.index("-ac") + 1], "2")
        self.assertEqual(result.name, "演奏.wav")

    def test_silence_has_empty_srt_and_matching_json_evidence(self):
        utility.save_subtitles(self.prepared, self.source, "zh", [])
        target = self.prepared / "钢琴 24bit.srt"
        self.assertEqual(target.read_text(), "")
        self.assertEqual(json.loads(target.with_suffix(".json").read_text())["segments"], [])

    def test_subtitle_basename_and_timestamps(self):
        rows = [dict(start=0.1, end=1.2, text="测试字幕", words=[])]
        utility.save_subtitles(self.prepared, self.source, "zh", rows)
        self.assertEqual((self.prepared / "钢琴 24bit.srt").read_text(), "1\n00:00:00,100 --> 00:00:01,200\n测试字幕\n")

    def test_existing_output_is_not_overwritten(self):
        current = self.root / "models/basic-pitch/current"
        current.mkdir(parents=True)
        (current / "receipt.json").write_text("{}")
        with self.assertRaises(FileExistsError):
            utility.execute(self.root, "basic-pitch", self.source, self.prepared, "auto")


if __name__ == "__main__":
    unittest.main()
