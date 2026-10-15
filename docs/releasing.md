# Releasing No Way Down

The prototype is English-only and credits funeralclef. The initial version is
0.1.0. Source lives in Git; downloadable builds belong in GitHub Releases.

## Build requirements

- Godot 4.7.2 .NET and its matching .NET export templates.
- .NET 9 SDK; the desktop target is .NET 8 and Android targets .NET 9.
- For Android: the `android` .NET workload, OpenJDK 17, Android platform 35,
  build-tools 35.0.1, and the SDK paths configured in Godot's editor settings.
- Python 3.11 or later and Git.

Run from the repository root:

```sh
python3 tools/export_release.py --platform windows
python3 tools/export_release.py --platform linux --output builds/linux-release
```

Use a fresh output directory for each invocation. To export all three targets,
set `GODOT_ANDROID_KEYSTORE_RELEASE_PATH`,
`GODOT_ANDROID_KEYSTORE_RELEASE_USER`, and
`GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD`, then run:

```sh
python3 tools/export_release.py --platform all --output builds/0.1.0
```

Keep the signing key and its password outside the repository. They are required
to sign compatible Android updates. The APK package ID is
`io.github.funeralclef.nowaydown`.

The script creates an isolated project without MCP, editor plugins or tests,
imports resources, exports release builds and packages the desktop runtimes.
Linux archives preserve the executable bit. `SHA256SUMS.txt` records each
download's checksum. Export logs and staging files remain under the ignored
output directory.

Publish the Windows ZIP, Linux tar.gz, signed Android APK and checksum file as
assets of the `v0.1.0` GitHub release. Do not commit generated builds or signing
credentials to Git.
