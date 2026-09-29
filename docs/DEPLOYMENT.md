# Build, deploy and release

## Build

`build.yml` runs on main pushes, pull requests and manual dispatch. The engine job is independent of Uno; browser compilation installs the pinned .NET/Uno toolchain and the WASM workload. `collect-site.py` requires both a generated index and an actual `.wasm` runtime. It cannot silently replace a failed publish with a static landing page.

The browser build is rooted at `/EffectsSpace/`. Local validation serves exactly that prefix. Read-only `?test=1` diagnostics expose current UI bounds and editor state to Playwright; there is no test-only mutation API. Tests use real pointer, keyboard and download events.

## GitHub Pages

`pages.yml` receives only successful main-branch Build completions. It rejects pull-request builds, checks the workflow path, verifies the current main SHA, downloads the matching browser artifact and compares `build-info.json`. Only that artifact is uploaded to Pages. The deployed URL is then tested with the same browser suite.

Pages must be available for the repository. The workflow requests automatic enablement through `actions/configure-pages`; organization policy or missing administration permissions can require an owner to select **Settings → Pages → GitHub Actions**. Such a setup error is not a successful deployment and must be reported as blocked.

Manual Pages dispatch selects the latest successful main Build. It still refuses to publish a stale commit. To deploy a new change, wait for its Build to pass rather than bypassing validation.

## Desktop

`desktop.yml` builds the shared native host on Windows, macOS and Linux. Those checks establish compilation, not a native UI acceptance run or signed distribution. The release workflow publishes self-contained single-file executables for win/linux/osx x64 and arm64. macOS signing/notarization and Windows signing are not configured by default.

## Release

Push a version tag such as `v0.1.0-alpha.1` to publish. The workflow validates the version, runs the engine/media/browser gates, builds browser and single-file desktop outputs, packs versioned libraries, and publishes a prerelease/release with checksums using the repository `GITHUB_TOKEN`. It then pushes the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) from the protected `nuget` environment: `NuGet/login` exchanges the job's OIDC token for a short-lived key, so no NuGet API key is stored. The `NUGET_USER` variable names the nuget.org account. A manual dispatch with a version input is a dry run that builds and uploads every asset as workflow artifacts but publishes nothing.

## Artifact inventory

- `EffectsSpace-source`: source snapshot of the built commit.
- `EffectsSpace-browser`: actual published Uno WASM site, including provenance.
- `EffectsSpace-packages`: reusable `.nupkg`/`.snupkg` libraries.
- `EffectsSpace-browser-validation`: screenshots, trace/report and browser test results.
- `EffectsSpace-public-site-validation`: the same validation against the live Pages URL.

Never use an artifact from a different commit to support a current-build claim. Software-rendered headless Chromium is a functional check; physical GPU performance should be measured separately.

## Media gates

Build and Release also run the portable-media and real-frame integration executables, then independently decode the generated AVI files with FFmpeg/ffprobe. FFmpeg is installed only as a CI verification tool and is not copied into application or library artifacts. Build uploads the original/generated media and `media-index.json` benchmark for inspection. Library packaging now produces eleven packages, including `EffectsSpace.Media`.
