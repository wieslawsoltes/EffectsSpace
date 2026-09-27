# EffectsSpace

**An independent motion-design and compositing workbench for Uno Platform.**

EffectsSpace brings a familiar composition viewer, layer timeline, keyframe editor, effect controls, project bins and render queue to a shared C# desktop and WebAssembly application. The implementation uses original code and assets, and is MIT licensed.

> Initial development release. EffectsSpace is not Adobe After Effects and does not claim complete feature, file-format or pixel parity. See the feature ledger and validation report for implemented behavior and explicit boundaries.

## Building

Install the .NET SDK selected by `global.json`. For browser builds, install `wasm-tools`. The engine libraries are ordinary .NET projects; the UI libraries and application use Uno's single-project SDK.

```sh
dotnet run --project tests/EffectsSpace.Tests -c Release
dotnet workload install wasm-tools
dotnet publish src/EffectsSpace.App -f net10.0-browserwasm -c Release
dotnet run --project src/EffectsSpace.App -f net10.0-desktop -p:EffectsSpaceDesktopOnly=true
```

## Architecture

The composition model, animation evaluation, validated document format, transactional editor, render planning, Skia renderer and custom Uno controls are reusable packages rather than application-only code. The application host owns platform storage and file dialogs.

## Independence

No Adobe binaries, source, icons, fonts, artwork or proprietary project-format implementations are included. Adobe and After Effects are trademarks of their respective owners. EffectsSpace is not affiliated with or endorsed by Adobe.

## License

MIT. Dependency notices and rendering tradeoffs are documented in `THIRD-PARTY-NOTICES.md` and `docs/ARCHITECTURE.md`.
