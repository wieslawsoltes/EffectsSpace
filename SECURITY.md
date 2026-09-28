# Security policy

EffectsSpace is a development release. Treat untrusted documents and image decoders as an attack surface. Do not use this release as a sandbox for executing arbitrary code; it deliberately contains no project scripting API.

## Safeguards

Projects are versioned JSON, not executable scripts. The loader enforces schema, collection, identifier, nesting, time and numeric limits, and checks parent/matte/composition cycles. Embedded images have encoded and decoded size budgets. Imported names are metadata and are never used as arbitrary filesystem paths. Scalar expressions have bounded length, operations, depth and numeric results and no host/network access.

Recovery remains on the user's device. The application does not send projects to an EffectsSpace server. GitHub Pages and its normal request logging are separate hosting infrastructure. Browser export uses user-initiated downloads; desktop export uses a native picker.

## Reporting

Use the repository's private vulnerability reporting feature when available. Do not place private project documents, credentials or personal media in a public issue. Include a minimized reproduction, affected commit, platform and whether the issue occurs before or after document validation.

## Boundaries

A validated document is not proof that every native image decoder or graphics driver is free of vulnerabilities. Keep the toolchain patched, preserve dependency notices and review transitive advisories. The initial release is unsigned on desktop. Full accessibility, hostile-document fuzzing and hardware-driver validation remain separate work.

## RIFF and time-based media

The portable media implementation validates RIFF/list/chunk bounds and padding, stream metadata, rational time bases, PCM block alignment and frame/sample counts. It does not execute project-supplied URLs or delegate arbitrary command lines to media tools. JPEG frame headers are checked against the AVI dimensions before Skia image creation. RIFF parsing is bounded, but native JPEG decoding remains a separate attack surface. FFmpeg is used only for test validation, never for opening projects or files in the application.

Imported encoded arrays must remain immutable after publication. Metadata/frame caches and history compare payload identity; callers must replace the array, not overwrite its contents. Browser preview uses a bounded 60-second audio buffer with cancellation/generation checks. Export checks byte, pixel and frame budgets. Review `docs/MEDIA.md` before broadening codec support or relaxing limits.
