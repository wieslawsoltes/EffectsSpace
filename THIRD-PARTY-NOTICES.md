# Third-party notices

EffectsSpace source is MIT licensed. Direct components and build tools have their own licenses; distributed packages retain the notices supplied by their publishers.

| Component | Purpose | License / source |
|---|---|---|
| .NET | Runtime and SDK | MIT and component notices — https://github.com/dotnet/runtime |
| Uno Platform / Uno SDK | Cross-platform C# UI and hosts | Apache-2.0 for Uno Platform core; inspect individual Uno packages — https://github.com/unoplatform/uno/blob/master/LICENSE |
| SkiaSharp | Managed graphics bindings | MIT — https://github.com/mono/SkiaSharp/blob/main/LICENSE.md |
| Skia | Native rendering | BSD-style — https://skia.googlesource.com/skia/+/main/LICENSE |
| Inter | Application/canvas typeface | SIL Open Font License 1.1 — fetched `Assets/Fonts/OFL.txt`, https://github.com/google/fonts/tree/main/ofl/inter |
| Playwright | Browser acceptance testing only | Apache-2.0 — https://github.com/microsoft/playwright/blob/main/LICENSE |
| GitHub Actions | CI and artifact/deployment tooling | Respective action repositories; not runtime application code |

The Inter font is downloaded at build time and its OFL notice is retained beside it. Original EffectsSpace icons and the ORBITAL sample are part of the MIT-licensed project.

No Adobe source, binary, icon, font, sample project or proprietary SDK is included. No FFmpeg, GPL media component or proprietary media codec implementation is bundled in this release. Operating-system/browser services are not relicensed by EffectsSpace.

Uno's ecosystem contains independently licensed packages. Consumers redistributing native/browser applications must preserve the actual notices accompanying their resolved dependency graph. Do not infer that every transitive package has the same license as its top-level package.
