# Local EN→VI evaluation — 2026-10-20

## Decision

Measurement phase completed. None of the seven measured configurations across four model families passes the dialogue-quality gate. Do not replace the current app provider with these models as a claimed quality fix. No app integration, OCR replacement or live cutscene benchmark was performed in this phase.

## Machine and scope

Intel Core i5-11400H, 12 logical processors, approximately 15.7 GiB physical RAM; Intel UHD and RTX 3050 Laptop GPU. Inference was CPU-only, INT8 with float32 accumulation, 2 threads, one resident translator, sequential requests. Minimal inference venv is `.tools/local-inference-venv`; conversion tools including CPU PyTorch are isolated in `.tools/local-translation-venv`. No global package installation, cloud translation, key access or game access.

16 distinct clean English sentences, repeated 3 times (48 warmed requests per reported run), from user-provided manga and one visually observed video subtitle. These are not 48 independent examples. No OCR/overlay timings are included. System background workloads were not controlled; measurements are indicative, not a statistically controlled comparison. Some measurement commands could overlap briefly; CPU figures are per process.

## Results

| Model/config | Warm p50 | Warm p95 | Process peak working set | Active process CPU, normalized over 12 logical CPUs |
|---|---:|---:|---:|---:|
| Argos EN→VI 1.9, beam 1 | 35.63 ms | 54.85 ms | 159.75 MiB | 17.36% |
| Helsinki OPUS-MT EN→VI, beam 2 | 44.23 ms | 85.62 ms | 161.38 MiB | 16.83% |
| NLLB-600M INT8, beam 1 | 282.31 ms | 529.18 ms | 1022.47 MiB | 16.44% |
| NLLB-600M INT8, beam 2 | 302.49 ms | 820.93 ms | 1022.38 MiB | 16.63% |

Process CPU is measured over a tight active translation loop, not the average CPU load during real dialogue playback. Model loading and the first request are excluded from warm percentiles and stored separately in JSON. Peak working set is the Windows process high-water mark; sampled RSS is also recorded. No GPU inference was requested. No game FPS or anti-cheat compatibility was measured.

## Quality review (manual, no numerical quality score)

- Argos: `I couldn't give less of a crap.` → `Tôi không thể không quan tâm.` This reverses the intended indifference. `gripes` is confused with gripping/grips.
- OPUS: `They rehash the same gripes...` → `Chúng lặp đi lặp lại những cái kẹp đó.` Still wrong. `The one who falls the hardest...` loses the romance context. `I couldn't give less of a crap.` → `Tôi không thể làm gì được.` Incorrect meaning.
- NLLB: `narcissistic tendencies` → `xu hướng tự kỷ` in both beam settings. Serious semantic error. `she up and died on me` is translated literally as standing up and dying on the speaker. Beam 2 repeats `chấn` in the poetic subtitle. Simple dialogue such as `You're so right` is more usable but does not offset these errors.
- Sample names Kabura/Haru/Nazuna are often retained, but this is not a name-preservation guarantee. No full glossary validation is integrated into this measurement.

Faster local translation is feasible on this CPU. The evaluated lightweight models do not demonstrate the semantic quality required for story dialogue. Larger/more contextual local models require a separate latency/RAM/VRAM assessment; do not assume the RTX 3050 can run one alongside the game without impact. Changing the translation model alone will not fix the already-demonstrated OCR omissions and incorrect bubble grouping.

## Follow-up: Qwen2.5 instruct models on CPU

To check whether a small multilingual chat model improves dialogue quality, two official Qwen2.5 Instruct GGUF models were run through llama.cpp with the same ten manga/cutscene sentences, a translation prompt and name-preservation instruction. Each sentence was called once; unlike the 48-request warmed benchmark above, these are single-pass indicative timings, not directly comparable latency statistics. CPU only, two threads, no GPU layers, server bound to loopback and stopped after each run.

| Configuration | Startup | Request p50 / p95 | Peak working set | Total for 10 calls |
|---|---:|---:|---:|---:|
| Qwen2.5-0.5B Instruct Q4_K_M | 1.66 s | 583 / 1,054 ms | 506.7 MiB | 6.21 s |
| Qwen2.5-1.5B Instruct Q4_K_M | 2.14 s | 1,254 / 4,830 ms | 1,679.3 MiB | 16.37 s |

Neither is suitable. The 0.5B model mistranslated `gripes` as eggs, emitted Portuguese for `crap`, refused to translate `dorks`, and left a difficult line in English. The 1.5B model produced unrelated wording for `gripes`, repeated tokens until its output limit on `falls the hardest`, and took 4.83 s for the p95 request. A short line (`Here you go!`) worked, but isolated easy examples do not meet the story-dialogue quality gate.

The specialized OPUS-MT model remains much faster (44 ms warmed p50) and uses less memory, but its mistranslation of `gripes` and the idiom `I couldn't give less of a crap` shows that speed alone does not meet the goal. Qwen's instruction following did not solve the idiom/context failures and made the CPU path substantially slower. The current evidence does not justify integrating either family as the default local translator.

## Follow-up: Qwen3-4B on RTX 3050

The next local-only gate used the official Qwen3-4B Q4_K_M GGUF with llama.cpp CUDA 12.4, all 36 layers offloaded, context 2048, and the same ten inputs. The model was checksum-verified against the official Hugging Face SHA-256 and ran on loopback only. Each sentence was requested once; percentiles are descriptive for this ten-sentence sample.

| Startup | Request p50 / p95 | Total for 10 calls | Peak working set | GPU memory used after load |
|---:|---:|---:|---:|---:|
| 3.24 s | 501 / 1,219 ms | 6.16 s | 2,874 MiB | 2,763 MiB of 4,096 MiB |

Some lines were more natural than the 0.5B/1.5B CPU models (`Haru's narcissistic tendencies` → `xu hướng tự phụ của Haru`; `Here you go!` → `Đây nhé!`). It still fails on story text: the `gripes` line ended with Chinese characters, `falls the hardest` remained literal, `I couldn't give less of a crap` was mistranslated, and `she up and died on me behind my back` became nonsense. A few usable lines do not offset those errors. Qwen3-4B therefore fails the quality gate despite GPU acceleration.

At the measured idle baseline, the model occupied about 2.76 GiB of the laptop GPU's 4 GiB. This leaves limited room for a game or other GPU workload; no game, FPS, or anti-cheat test was performed. The full evaluation is not a real-time playback test. Given that neither local quality nor shared-GPU headroom is established, stop local-only model integration here. Ask whether the user will accept an optional online translation provider before implementing a hybrid path.

## Provenance and preparation issues

- Argos package: https://github.com/argosopentech/argospm-index ; public index selected `https://argos-net.com/v1/translate-en_vi-1_9.argosmodel` (67,770,159 bytes). Hash is recorded in its benchmark JSON.
- Helsinki model: https://huggingface.co/Helsinki-NLP/opus-mt-en-vi . Original Marian URL in the model card returned NoSuchKey; used official Hugging Face PyTorch weights, verified their published SHA-256, converted offline to INT8. Commit and source hash: `artifacts/local-benchmark/opus-source.json`.
- Meta model: https://huggingface.co/facebook/nllb-200-distilled-600M . Source weights 2,460,457,927 bytes; verified published SHA-256 before offline conversion. INT8 model.bin 622,596,105 bytes. Source commit/hash: `artifacts/local-benchmark/nllb-source.json`. Model card states CC-BY-NC-4.0 and research intent; this evaluation is for the user's personal experiment, not a production/commercial redistribution decision.
- NLLB tokenizer reconstruction from only SentencePiece shifted language IDs by one. This produced wrong-language output in initial runs; those runs are invalid and were superseded. Downloaded the official tokenizer.json at the same pinned commit, verified IDs eng_Latn=256047, vie_Latn=256193 and regenerated vocabulary/model before the reported runs. No remote model code was enabled.
- CPU INT8 guidance: https://opennmt.net/CTranslate2/performance.html . Token protocol: https://opennmt.net/CTranslate2/guides/transformers.html .

## Artifacts and next gate

- Reproducible script: `scripts/benchmark-local-translation.py` (requires downloaded model/runtime).
- Raw results: `artifacts/local-benchmark/argos-translation-beam1.json`, `opus-translation-beam2.json`, `nllb-translation-beam1.json`, `nllb-translation-beam2.json`.
- Qwen results: `artifacts/local-llm-evaluation/qwen2.5-0.5b-cpu.json` and `qwen2.5-1.5b-cpu.json`; pinned GGUF files and llama.cpp runtime remain under `models/local-chat/` and `.tools/`.
- Qwen3 GPU result: `artifacts/local-llm-evaluation/qwen3-4b-gpu.json`; reproducible harness `scripts/evaluate-qwen3-gpu.ps1`; model/runtime are under `models/local-chat/` and `.tools/`.
- Older unprefixed translation-beam1/2 JSON belong to earlier Argos runs and are historical. Use the model-prefixed files above.
- Weights and model sources remain under `models/`; models/ is ignored by Git. Conversion tools/source weights consume more disk than the inference model; no cleanup of downloaded source files has been performed.
- Before integrating any local engine: select a model that passes dialogue meaning checks without hardcoding this sample, replace/compare OCR on the supplied frames, implement whole-subtitle latest-only processing, and measure end-to-end latency on https://youtu.be/5mCH8zIhQ4s at normal playback. A cutscene frame was observed at approximately 00:26, with English text in the upper subtitle band; no full-video OCR/translation test was run.
