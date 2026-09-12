"""Generate a short synthetic note sequence for repeatable local acceptance."""
import math
from pathlib import Path
import struct
import sys
import wave

path = Path(sys.argv[1])
path.parent.mkdir(parents=True, exist_ok=True)
rate = 44100
samples = []
for midi in [60, 64, 67, 72, 67, 64, 62, 65, 69, 74, 69, 65]:
    frequency = 440 * 2 ** ((midi - 69) / 12)
    for index in range(int(rate * .65)):
        t = index / rate
        envelope = min(t / .012, 1) * math.exp(-4 * t) * min((.65 - t) / .06, 1)
        value = sum(weight * math.sin(2 * math.pi * frequency * harmonic * t)
                    for harmonic, weight in [(1, .65), (2, .2), (3, .1), (4, .05)])
        samples.append(struct.pack("<h", int(16000 * envelope * value)))
    samples.extend([b"\0\0"] * int(.15 * rate))
with wave.open(str(path), "wb") as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes(b"".join(samples))
print(path, "synthetic notes:", len(samples) / rate, "seconds")
