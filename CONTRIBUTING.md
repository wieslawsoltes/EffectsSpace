# Contributing

Use the pinned SDK and keep dependency direction intact: models must not depend on Uno or Skia. Add tests alongside every change to time evaluation, validation, editing or rendering. Keep classes in individual files and avoid application-only logic in reusable packages.

Run engine and renderer tests, build the browser application and run browser interactions before opening a pull request. A UI change should include a current screenshot or trace from the actual Uno application, not a static design reference. Update the feature ledger when adding or narrowing behavior.

Use atomic editor transactions. Pointer drags must support cancel/lost-capture rollback. Never silently omit unsupported media during export. Treat document identifiers, asset sizes, nesting, decoded image dimensions and expression limits as security boundaries.

Do not add proprietary Adobe assets, reverse-engineered binary distributions, copied UI screenshots as application content, or dependencies that conflict with the permissive distribution goal. State performance claims with the hardware, backend, test input and methodology used.

Dependency upgrades must keep managed and native Skia on the same ABI. Do not blindly upgrade only the SkiaSharp NuGet package beyond the version expected by the selected Uno SDK.
