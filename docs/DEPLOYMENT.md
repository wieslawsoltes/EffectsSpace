# Build, deploy and release

## Build

`build.yml` runs on main pushes, pull requests and manual dispatch. The engine job is independent of Uno; browser compilation installs the pinned .NET/Uno toolchain and the WASM workload. `collect-site.py` requires both a generated index and an actual `.wasm` runtime. It cannot silently replace a failed publish with a static landing page.

The browser build is rooted at `/EffectsSpace/`. Local validation serves exactly that prefix. Read-only `?test=1` diagnostics expose current UI bounds and editor state to Playwright; there is no test-only mutation API. Tests use real pointer, keyboard and download events.

## GitHub Pages

`pages.yml` receives only successful main-branch Build completions. It rejects pull-request builds, checks the workflow path, verifies the current main SHA, downloads the matching browser artifact and compares `build-info.json`. Only that artifact is uploaded to Pages. The deployed URL is then tested with the same browser suite.

Pages must be available for the repository. The workflow requests automatic enablement through `actions/configure-pages`; organization policy or missing administration permissions can require an owner to select **Settings → Pages → GitHub Actions**. Such a setup error is not a successful deployment and must be reported as blocked.

Manual Pages dispatch selects the latest successful main Build. It still refuses to publish a stale commit. To deploy a new change, wait for its Build to pass rather than bypassing validation.

## Desktop

`desktop.yml` builds the shared native host on Windows, macOS and Linux. Those checks establish compilation, not a native UI acceptance run or signed distribution. The release workflow publishes self-contained desktop bundles for selected runtime identifiers. macOS signing/notarization and Windows signing are not configured by default.

## Release

Push a version tag such as `v0.1.0-alpha.1`, or use the release workflow's manual input. The workflow validates the requested tag, builds browser and native outputs, packs libraries, and publishes a prerelease/release with checksums. It uses the repository `GITHUB_TOKEN`, not a credential embedded in source. Publishing to an external NuGet feed is a separate explicit workflow and requires the owner to configure `NUGET_API_KEY`.

## Artifact inventory

- `EffectsSpace-source`: source snapshot of the built commit.
- `EffectsSpace-browser`: actual published Uno WASM site, including provenance.
- `EffectsSpace-packages`: reusable `.nupkg`/`.snupkg` libraries.
- `EffectsSpace-browser-validation`: screenshots, trace/report and browser test results.
- `EffectsSpace-public-site-validation`: the same validation against the live Pages URL.

Never use an artifact from a different commit to support a current-build claim. Software-rendered headless Chromium is a functional check; physical GPU performance should be measured separately.
