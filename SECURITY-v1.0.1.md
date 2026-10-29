# Security and privacy notes for v1.0.1

## Update notifications

EberoNest v1.0.1 adds a read-only release check against the official GitHub repository, `Efzax/EberoNest`, using the GitHub latest-release API over HTTPS. Normal TLS certificate validation remains enabled. The response is size-limited, parsed as release metadata, and never used to download or execute release assets.

The automatic check runs in the background at application startup and is attempted at most once per local calendar day after a successful response. There is no polling timer. A user can also start an unrestricted manual check from About. Automatic failures are silent, and update-check failures are isolated from file organization.

The request includes the application name and current version in its standard HTTP User-Agent, plus ordinary connection metadata required to contact GitHub. It does not include the Windows username, local paths, rules, files, Downloads contents, configured extensions, a device identifier, telemetry, or analytics. The application does not auto-update, auto-install, download assets, or execute received content. A release link is opened only after validating HTTPS, the exact `github.com` host, owner `Efzax`, repository `EberoNest`, and a path matching the parsed stable version tag.

The only new local state is the date of the last successful automatic check, saved separately at `%LOCALAPPDATA%\EberoNest\last-update-check.txt`. Missing or malformed state is treated as absent and never affects `rules.json`.

## Scope

This note documents the new network and privacy behavior introduced in v1.0.1. It does not reopen other v1.0.0 security decisions.
