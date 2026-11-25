"""Measure a local model only. No screenshot capture, game access, provider key or cloud calls."""
from pathlib import Path
import argparse
import hashlib
import importlib.metadata
import json
import statistics
import threading
import time
import zipfile

import ctranslate2
import psutil
import sentencepiece

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT / "models/local-en-vi/translate-en_vi-1_9.argosmodel"
EXTRACTED = ROOT / "models/local-en-vi/unpacked"
SAMPLES = [
    "You hear me, Kabura?",
    "They rehash the same gripes over and over and over again.",
    "I know, I know. The one who falls the hardest always loses.",
    "But I can't believe she up and died on me behind my back!",
    "Yeah, true, but...",
    "It all boils down to Haru's narcissistic tendencies.",
    "Not even their reactions change.",
    "Yeah! You're so right!",
    "I know!!",
    "That's what was so cute about her.",
    "And they always come to the same conclusion.",
    "Don't you agree, Nazuna?",
    "I couldn't give less of a crap.",
    "Why the hell do I hang out with these dorks every night?",
    "The wind shakes laden frost; the umbrella's ribs groan.",
    "Are you listening, Kabura? Heyyy!",
]


def unpack():
    EXTRACTED.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ARCHIVE) as archive:
        for entry in archive.infolist():
            parts = Path(entry.filename).parts
            if len(parts) < 2 or (parts[1] not in {"model", "metadata.json", "README.md", "sentencepiece.model"}):
                continue
            destination = (EXTRACTED / entry.filename).resolve()
            if not destination.is_relative_to(EXTRACTED.resolve()):
                raise ValueError("Archive path escapes model directory")
            if entry.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
            elif not destination.exists():
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(archive.read(entry))
    return EXTRACTED / "translate-en_vi-1_9"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--beam", type=int, choices=[1, 2], default=1)
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--repeats", type=int, default=2)
    parser.add_argument("--model", choices=["argos", "opus", "nllb"], default="argos")
    args = parser.parse_args()
    folder = unpack() if args.model == "argos" else ROOT / f"models/{args.model}-en-vi/ct2-int8"
    process = psutil.Process()
    baseline = process.memory_info().rss
    peak = [baseline]
    stop = threading.Event()

    def sample_memory():
        while not stop.wait(.02):
            peak[0] = max(peak[0], process.memory_info().rss)

    sampler = threading.Thread(target=sample_memory, daemon=True)
    sampler.start()
    started = time.perf_counter()
    tokenizer_file = {"argos": "sentencepiece.model", "opus": "source.spm", "nllb": "sentencepiece.bpe.model"}[args.model]
    tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(folder / tokenizer_file))
    target_tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(folder / "target.spm")) if args.model == "opus" else tokenizer
    translator = ctranslate2.Translator(str(folder / "model" if args.model == "argos" else folder), device="cpu", compute_type="int8",
                                      inter_threads=1, intra_threads=args.threads)
    load_ms = (time.perf_counter() - started) * 1000

    def translate(text):
        tokens = tokenizer.encode(text, out_type=str)
        if args.model == "opus":
            tokens = [">>vie<<"] + tokens + ["</s>"]
        options = {}
        if args.model == "nllb":
            tokens = ["eng_Latn"] + tokens + ["</s>"]
            options["target_prefix"] = [["vie_Latn"]]
        result = translator.translate_batch([tokens], beam_size=args.beam, max_decoding_length=192, **options)[0]
        pieces = result.hypotheses[0][1:] if args.model == "nllb" else result.hypotheses[0]
        # Argos SentencePiece decoding also normalizes residual word-boundary markers.
        return target_tokenizer.decode(pieces).replace("▁", " ").strip()

    started = time.perf_counter()
    first_output = translate(SAMPLES[0])
    first_ms = (time.perf_counter() - started) * 1000
    records = []
    cpu_start = process.cpu_times()
    wall_start = time.perf_counter()
    for repeat in range(args.repeats):
        for index, source in enumerate(SAMPLES):
            started = time.perf_counter()
            translated = translate(source)
            records.append({"repeat": repeat, "source": source, "translation": translated,
                            "milliseconds": round((time.perf_counter() - started) * 1000, 2)})
            if (index + 1) % 8 == 0:
                print(f"beam={args.beam} repeat={repeat + 1} sentences={index + 1}", flush=True)
    wall = time.perf_counter() - wall_start
    cpu_end = process.cpu_times()
    cpu = cpu_end.user + cpu_end.system - cpu_start.user - cpu_start.system
    stop.set()
    sampler.join()
    memory = process.memory_info()
    peak[0] = max(peak[0], memory.rss)
    latencies = sorted(record["milliseconds"] for record in records)
    metrics = {
        "device": "cpu", "compute_type": translator.compute_type, "threads": args.threads,
        "beam_size": args.beam, "warm_sentence_count": len(records),
        "distinct_sentences": len(SAMPLES), "repeats": args.repeats,
        "model_load_ms": round(load_ms, 2), "first_translation_ms": round(first_ms, 2),
        "warm_p50_ms": round(statistics.median(latencies), 2),
        "warm_p95_ms": latencies[max(0, int(len(latencies) * .95 + .999999) - 1)],
        "warm_max_ms": max(latencies), "warm_wall_seconds": round(wall, 3),
        "process_cpu_seconds": round(cpu, 3),
        "mean_cpu_percent_one_core_100": round(cpu / wall * 100, 2),
        "mean_cpu_percent_machine": round(cpu / wall * 100 / psutil.cpu_count(), 2),
        "baseline_rss_mib": round(baseline / 2**20, 2),
        "sampled_peak_rss_mib": round(peak[0] / 2**20, 2),
        "process_peak_working_set_mib": round(getattr(memory, "peak_wset", peak[0]) / 2**20, 2),
    }
    archive = ARCHIVE if args.model == "argos" else folder / "model.bin"
    with archive.open("rb") as file:
        digest = hashlib.file_digest(file, "sha256").hexdigest()
    report = {
        "scope": "Translation only, clean text, warmed model, no OCR/overlay/game/FPS measurement",
        "source": {"argos": "https://argos-net.com/v1/translate-en_vi-1_9.argosmodel", "opus": "https://huggingface.co/Helsinki-NLP/opus-mt-en-vi", "nllb": "https://huggingface.co/facebook/nllb-200-distilled-600M"}[args.model],
        "artifact_bytes": archive.stat().st_size,
        "artifact_sha256": digest,
        "packages": {name: importlib.metadata.version(name) for name in ["ctranslate2", "sentencepiece", "numpy", "psutil"]},
        "metrics": metrics, "first_translation": first_output, "results": records,
    }
    output = ROOT / f"artifacts/local-benchmark/{args.model}-translation-beam{args.beam}.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(metrics, indent=2), flush=True)


if __name__ == "__main__":
    main()
