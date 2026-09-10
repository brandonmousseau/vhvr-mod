#!/usr/bin/env python3
"""Prepare stripped build references outside the game installation. Requires Python 3 and .NET 8."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("managed", type=Path, help="Valheim/valheim_Data/Managed")
    parser.add_argument("output", type=Path, help="Dedicated directory outside the game installation")
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    managed, output = args.managed.resolve(), args.output.resolve()
    if not (managed / "assembly_valheim.dll").is_file():
        parser.error("The managed directory does not contain assembly_valheim.dll")
    if output == managed or managed in output.parents:
        parser.error("Build references must be stored outside the game's Managed directory")
    patterns = ["UnityEngine*.dll", "assembly_*.dll", "Unity.InputSystem.dll", "Unity.TextMeshPro.dll",
                "gui_framework.dll", "PlayFab*.dll", "SoftReferenceableAssets.dll", "Splatform*.dll",
                "com.rlabrecque.steamworks.net.dll", "Newtonsoft.Json.dll"]
    inputs = sorted({path for pattern in patterns for path in managed.glob(pattern)})
    with tempfile.TemporaryDirectory(prefix="vhvr-references-") as temp:
        temp = Path(temp)
        tools = temp / "tools"
        subprocess.run([args.dotnet, "tool", "install", "BepInEx.AssemblyPublicizer.Cli",
                        "--version", "0.4.3", "--tool-path", str(tools)], check=True)
        cli = next(tools.glob(".store/**/tools/net6.0/any/BepInEx.AssemblyPublicizer.Cli.dll"))
        stage = temp / "references"
        subprocess.run([args.dotnet, str(cli), *map(str, inputs), "-o", str(stage),
                        "--strip", "--dont-add-attribute"], check=True,
                       env=dict(os.environ, DOTNET_ROLL_FORWARD="Major"))
        generated = sorted(stage.glob("*-publicized.dll"))
        if len(generated) != len(inputs):
            raise RuntimeError("Publicizer did not produce a reference for every input assembly")
        output.mkdir(parents=True, exist_ok=True)
        manifest = output / "references.json"
        old_files = json.loads(manifest.read_text())["files"] if manifest.exists() else []
        for filename in old_files:
            if Path(filename).name != filename or not filename.endswith("-publicized.dll"):
                raise RuntimeError("Invalid previous reference manifest")
        for filename in old_files:
            (output / filename).unlink(missing_ok=True)
        for path in generated:
            shutil.copyfile(path, output / path.name)
        manifest.write_text(json.dumps({"game_managed": str(managed), "publicizer": "0.4.3",
                                       "files": [path.name for path in generated]}, indent=2) + "\n")
        print(f"Prepared {len(generated)} build-only references in {output}")


if __name__ == "__main__":
    main()
