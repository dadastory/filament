# Mobile EASU validation

This is the review evidence for the proposed change to
`filament/src/materials/fsr/ffx_fsr1_mobile.fs`. The patch branch contains
the shader change. This branch holds the benchmark harness and results.

## What changes

The existing flat-region decision runs before the edge-shape parameters are
computed. The comparison threshold and the center-sample return are the same.
For the full filter path, the patch schedules some texture gathers later and
keeps the tap order, weights, and deringing logic. It changes neither the scene
setup nor the material interface.

## Android runtime evidence

The device used Vulkan on Mali-G925-Immortalis MC12 (MT6991). The runtime and
the host compiler were from Filament v1.75.0, commit
`0e58877c`. The scenes were the upstream DamagedHelmet orbit and Fox Walk.
The renderer used a 1920 × 1080 output, a fixed 2/3 render scale, 30 warmup
frames, and 120 timed frames for each run.

- [Shader disassembly](results/v175-stock-fragment.spvasm) and the
  [candidate disassembly](results/v175-candidate-fragment.spvasm) show 44 vs
  21 floating-point, comparison, and extended arithmetic operations before
  the flat-region branch. These are SPIR-V counts rather than device ISA
  counters.
- [Matched RGB frames](results/helmet-orbit-rgb-sha256.csv) record identical
  decoded RGB for all 120 frames of the DamagedHelmet orbit.
- [FLIP results](results/flip-v175.json) record equal average and worst-frame
  LDR-FLIP error in both scenes, with 12 matched frames per scene. The
  reference was the corresponding native 1920 × 1080 render, at 67 pixels
  per degree.

The first search selected this candidate at **9.20%** lower GPU frame time
under the platform aggregation. The follow-up run used eight balanced A/B
pairs and four A/A pairs per scene, with both orderings of stock and
candidate. It produced **0.87%** under the same aggregation.

| Scene | Median A/B P50 reduction | Candidate faster | Median absolute A/A change |
| --- | ---: | ---: | ---: |
| DamagedHelmet | 3.22% | 6 / 8 | 7.25% |
| Fox | -1.48% | 3 / 8 | 1.52% |

Positive reduction means lower GPU frame time. Each pair compares the
median of its 120 GPU frame times. The aggregate is the mean of the two
scene medians. The follow-up results are within the same-build timing
variation observed in these scenes and do not establish a reliable speedup.

See the [original trial](results/original-trial-v175.json),
the [original timing logs](results/original-raw-timing),
[all 48 timing logs](results/raw-timing), the
[per-pair CSV](results/paired-timings.csv), and the
[summary](results/summary.json).

## Patch and compiler coverage

The patch is based on upstream commit
`863e7d72420e99f4efbed8cafa6a343f3407018d`. Its baseline
`ffx_fsr1_mobile.fs` matches the v1.75.0 shader byte for byte. The enclosing
material on main now uses the shared texture-gather helper. The reported
runtime tests used the v1.75.0 material wrappers.

The v1.75.0 host tool compiled the baseline and patched versions of both
`fsr_easu_mobile.mat` and `fsr_easu_mobileF.mat` for OpenGL and Vulkan.
`matinfo` validated the Vulkan fragment variants, and the patch reproduced
the four single-pass Vulkan shader disassemblies from the timed Android
packages. Runtime and compiler testing with the current main material wrappers
is still pending.

## Reproduce the v1.75.0 run

1. Check out official Filament v1.75.0. Extract the matching Linux host and
   Android native release archives. Use an Android NDK with the arm64 target.
2. Build the harness from this branch. Point the paths below at those releases
   and the source checkout.

   ```sh
   cmake -S benchmark -B build-android -G Ninja \
     -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
     -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 \
     -DANDROID_STL=c++_static -DCMAKE_BUILD_TYPE=Release \
     -DFILAMENT_SDK="$FILAMENT_ANDROID" -DFILAMENT_SOURCE="$FILAMENT_SRC" \
     -DFILAMENT_HOST_TOOLS="$FILAMENT_HOST/bin"
   cmake --build build-android
   ```

   Set `FILAMENT_FLIP_INCLUDE` to the directory containing the FLIP 1.7
   `FLIP.h` to reproduce perceptual error reports.
3. Copy the v1.75.0 `filament/src/materials/fsr` directory to a scratch
   directory. Replace just its `ffx_fsr1_mobile.fs` with the file on
   [the patch branch](https://github.com/dadastory/filament/tree/perf/mobile-easu-flat-path).
   Compile the candidate material in that scratch directory.

   ```sh
   "$FILAMENT_HOST/bin/matc" -a opengl -a vulkan -p mobile \
     -o candidate.filamat fsr_easu_mobile.mat
   ```

4. On the Android device, place the built executable as `assets/runner`.
   Use `DamagedHelmet.glb` and `Fox.glb` from the official checkout, and
   place `lightroom_14b_ibl.ktx` from the host release as
   `assets/environment.ktx`. Place `candidate.filamat` and
   `measure-ab-aa.sh` outside the assets directory. Make `assets/runner`
   executable. In the following example, `$case_dir` is the scratch
   directory on the device.

   ```sh
   adb shell sh "$case_dir/measure-ab-aa.sh" "$case_dir" "$case_dir/assets"
   adb pull "$case_dir/timings.tar" .
   ```

   For FLIP, run `measure.sh` instead, with
   `"$case_dir" "$case_dir/assets" keep-display flip` as arguments and the
   FLIP-enabled harness. For the full 120-frame orbit, set
   `CAPTURE_STEP = 1` in the scratch harness, leave
   `LLM4AD_FSR_TIMINGS_ONLY` unset, and compare the decoded RGB buffers
   for identical frame numbers. Readbacks run after the timing phase.

The benchmark harness is from the LLM4AD Next Filament evaluator example.
Its license is retained in [benchmark/LICENSE](benchmark/LICENSE).
