"""Source gates for the SteamVR-only extraction; does not certify headset parity."""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = '0604e05a3284ba373f3630d2e4d473370a254663'
CORE = ROOT / 'ValheimVRMod'

def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args]).decode('utf-8-sig')

def require(condition, label):
    if not condition:
        raise AssertionError(label)

def code_only(text):
    # Skip C# comments and string/character literals when checking SDK references.
    return re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', ' ', text)

def body(text, name):
    match = re.search(r'(?:public|private)\s+(?:static\s+)?[\w<>]+\s+' + name + r'\s*\(', text)
    require(match is not None, 'missing method ' + name)
    start = text.index('{', match.end())
    depth, end = 1, start + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return re.sub(r'\s+', '', text[start:end])

require(not git('diff', BASE, '--', 'Unity', 'ValheimVRMod/ValheimVRMod.csproj',
                'ValheimVRMod/ValheimVRMod.cs', 'ValheimVRMod/Properties').strip(),
        'SDK, generated actions, assets, project target or plugin version changed')
for relative in ['Utilities/ApplicationManifestHelper.cs', 'VRCore/BodyTracking/SteamVRBodyTrackingProvider.cs']:
    require(git('show', BASE + ':ValheimVRMod/' + relative).replace('\r\n', '\n') ==
            (CORE / 'VRCore/Backends' / Path(relative).name).read_text(encoding='utf-8-sig'),
            'moved provider source changed: ' + relative)

for path in CORE.rglob('*.cs'):
    if any(part in path.parts for part in ['Backends', 'obj', 'bin']):
        continue
    code = code_only(path.read_text(encoding='utf-8-sig'))
    require(not re.search(r'Valve\.VR|Unity\.XR\.OpenVR|\bSteamVR_\w+|\bOpenVR\.|\bCVR\w+|\bEVR\w+|\bVREvent_\w+|\bETracked\w+', code),
            'SDK reference outside the provider boundary: ' + str(path.relative_to(ROOT)))

original = git('show', BASE + ':ValheimVRMod/VRCore/VRManager.cs')
extracted = (CORE / 'VRCore/Backends/SteamVRRuntime.cs').read_text(encoding='utf-8-sig')
extracted = extracted.replace('NativeMirrorMode(VHVRConfig.GetMirrorViewMode())', 'VHVRConfig.GetMirrorViewMode()')
methods = re.findall(r'(?:public|private) static [\w<>]+ (\w+)\(', original)
for name in methods:
    require(body(original, name) == body(extracted, name), 'runtime method body changed: ' + name)

# The 42 supported actions and both sets must use the exact upstream path spelling.
catalog = git('show', BASE + ':Unity/ValheimVR/Assets/SteamVR_Input/SteamVR_Input_Actions.cs')
expected = {name: path for name, kind, path in re.findall(
    r'SteamVR_Actions\.p_(\w+)\s*=.*?SteamVR_Action\.Create<SteamVR_Action_(\w+)>\("([^"]+)"\)', catalog)
    if kind in {'Boolean', 'Vector2', 'Pose', 'Vibration'}}
set_catalog = git('show', BASE + ':Unity/ValheimVR/Assets/SteamVR_Input/SteamVR_Input_ActionSets.cs')
expected.update(re.findall(r'SteamVR_Actions\.p_(\w+)\s*=.*?SteamVR_ActionSet\.Create<[^>]+>\("([^"]+)"\)', set_catalog))
facade = (CORE / 'VRCore/Backends/VRInputActions.cs').read_text()
actual = dict(re.findall(r'public static \w+ (\w+) => VRBackend.Active.Input.\w+\("([^"]+)"\)', facade))
require(expected == actual, 'action catalog differs from upstream')
require(not (ROOT / 'OpenXR').exists(), 'OpenXR implementation is outside stage 1')
print(f'PASS: provider boundary, {len(methods)} unchanged runtime method bodies, exact action paths, and unchanged upstream SDK/assets/project/version.')
