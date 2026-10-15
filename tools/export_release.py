#!/usr/bin/env python3
"""Export the prototype from a clean staging project, without editor tooling."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]
VERSION = "0.1.0"
TARGETS = {
    "windows": ("Windows Desktop", "NoWayDown.exe"),
    "linux": ("Linux", "NoWayDown.x86_64"),
    "android": ("Android", f"No-Way-Down-{VERSION}-android-arm64.apk"),
}


def run(command, log, cwd):
    print("Running:", " ".join(map(str, command)), flush=True)
    with log.open("w") as stream:
        subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT, check=True)


def prepare(stage):
    stage.mkdir(parents=True, exist_ok=False)
    names = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT
    ).decode().split("\0")
    for name in names:
        if not name or Path(name).parts[0] in {"addons", "tests", "docs", "tools"}:
            continue
        source = ROOT / name
        if source.is_file():
            destination = stage / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
    project = stage / "project.godot"
    lines = project.read_text().splitlines()
    lines = [line for line in lines if not line.startswith("MCPRuntimeServer=")]
    lines = ["enabled=PackedStringArray()" if line.startswith("enabled=PackedStringArray(") else line for line in lines]
    project.write_text("\n".join(lines) + "\n")


def package(target, folder, output):
    if target == "android":
        artifact = output / TARGETS[target][1]
        shutil.copy2(folder / artifact.name, artifact)
        return artifact
    # Preserve the font's license alongside the game and bundled .NET runtime notices.
    licenses = folder / "licenses"
    licenses.mkdir(exist_ok=True)
    shutil.copytree(ROOT / "licenses", licenses, dirs_exist_ok=True)
    shutil.copy2(ROOT / "fonts/LICENSE.txt", licenses / "Montserrat-OFL.txt")
    if target == "windows":
        artifact = output / f"No-Way-Down-{VERSION}-windows-x64.zip"
        with zipfile.ZipFile(artifact, "w", zipfile.ZIP_DEFLATED) as archive:
            for path in sorted(folder.rglob("*")):
                if path.is_file():
                    archive.write(path, Path("No Way Down") / path.relative_to(folder))
    else:
        (folder / TARGETS[target][1]).chmod(0o755)
        artifact = output / f"No-Way-Down-{VERSION}-linux-x64.tar.gz"
        with tarfile.open(artifact, "w:gz") as archive:
            archive.add(folder, arcname="No Way Down")
    return artifact


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=["all", *TARGETS], default="all")
    parser.add_argument("--output", type=Path, default=ROOT / "builds" / VERSION)
    parser.add_argument("--godot", default="godot-mono")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / ".gdignore").touch()
    stage = output / "project"
    # A previous export remains intact; require an explicit fresh output folder.
    prepare(stage)
    run(["dotnet", "build", "No Way Down.csproj", "-p:UseSharedCompilation=false"], output / "build.log", stage)
    run([args.godot, "--headless", "--editor", "--path", str(stage), "--import"], output / "import.log", stage)
    artifacts = []
    targets = TARGETS if args.platform == "all" else [args.platform]
    for target in targets:
        if target == "android" and not os.environ.get("GODOT_ANDROID_KEYSTORE_RELEASE_PATH"):
            raise SystemExit("Set the Godot Android release keystore environment variables before exporting Android.")
        folder = output / target
        folder.mkdir(exist_ok=True)
        preset, filename = TARGETS[target]
        run([args.godot, "--headless", "--path", str(stage), "--export-release", preset, str(folder / filename)], output / f"{target}.log", stage)
        artifacts.append(package(target, folder, output))
    (output / "SHA256SUMS.txt").write_text("".join(
        f"{hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()}  {path.name}\n" for path in artifacts
    ))
    print("Release artifacts:", *(str(path) for path in artifacts), sep="\n", flush=True)


if __name__ == "__main__":
    main()
