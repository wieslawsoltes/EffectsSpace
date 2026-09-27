# Security policy

EffectsSpace is a development release. Treat untrusted documents and image decoders as an attack surface. Do not use this release as a sandbox for executing arbitrary code; it deliberately contains no project scripting API.

## Safeguards

Projects are versioned JSON, not executable scripts. The loader enforces schema, collection, identifier, nesting, time and numeric limits, and checks parent/matte/composition cycles. Embedded images have encoded and decoded size budgets. Imported names are metadata and are never used as arbitrary filesystem paths. Scalar expressions have bounded length, operations, depth and numeric results and no host/network access.

Recovery remains on the user's device. The application does not send projects to an EffectsSpace server. GitHub Pages and its normal request logging are separate hosting infrastructure. Browser export uses user-initiated downloads; desktop export uses a native picker.

## Reporting

Use the repository's private vulnerability reporting feature when available. Do not place private project documents, credentials or personal media in a public issue. Include a minimized reproduction, affected commit, platform and whether the issue occurs before or after document validation.

## Boundaries

A validated document is not proof that every native image decoder or graphics driver is free of vulnerabilities. Keep the toolchain patched, preserve dependency notices and review transitive advisories. The initial release is unsigned on desktop. Full accessibility, hostile-document fuzzing and hardware-driver validation remain separate work.
