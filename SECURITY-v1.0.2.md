# Security and privacy notes for v1.0.2

## Language preference

The user's Spanish or English selection is stored in `%LOCALAPPDATA%\EberoNest\language.txt`, separately from `rules.json`. The value is limited to `es` or `en`; a missing, unreadable, or malformed value falls back to the Windows display language. User-created rule names and folders are treated as data and are not translated or rewritten.

## About links

About opens only the canonical `https://github.com/Efzax/EberoNest` repository URL. Validation requires HTTPS, the exact GitHub host and path, no credentials, nonstandard port, query, or fragment. Release links continue to use the existing v1.0.1 validation policy.

## Native title bar

The application retains native WinForms window borders and system controls. It requests dark title-bar and caption colors through Windows DWM attributes, with unsupported-attribute failures handled without replacing native window behavior.

## Windows Recent history

The observed profile item `Recent\EberoNest.lnk` targeted the public repository folder, had no arguments, and did not target the installed application. The actual Startup shortcut targeted the installed `EberoNest.exe` with `--background`. This identifies the Recent item as a separate Explorer history entry rather than the Startup shortcut. v1.0.2 does not delete or modify that profile item. Startup shortcut creation and repair use the existing Windows Script Host shortcut API and do not call APIs that add documents to Recent history; Verification checks this in an isolated Startup directory and confirms the profile's same-named Recent item remains unchanged.

## Scope

This note covers only the new language preference, About repository link, DWM title-bar styling, and Recent-history investigation in v1.0.2. It does not reopen the locked security behavior from v1.0.0 or v1.0.1.
