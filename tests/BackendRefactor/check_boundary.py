"""Source gates for the SteamVR-only extraction; does not certify headset parity."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[2]
CORE = ROOT / 'ValheimVRMod'

def require(condition, label):
    if not condition:
        raise AssertionError(label)

def code_only(text):
    # Skip C# comments and string/character literals when checking SDK references.
    return re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', ' ', text)

for path in CORE.rglob('*.cs'):
    if any(part in path.parts for part in ['Backends', 'obj', 'bin']):
        continue
    code = code_only(path.read_text(encoding='utf-8-sig'))
    require(not re.search(r'Valve\.VR|Unity\.XR\.OpenVR|\bSteamVR_\w+|\bOpenVR\.|\bCVR\w+|\bEVR\w+|\bVREvent_\w+|\bETracked\w+', code),
            'SDK reference outside the provider boundary: ' + str(path.relative_to(ROOT)))

# The action catalog is the one list of actions; the shipped actions.json must declare the same sets and actions
# with the same types and requirements. SteamVR_Actions (the generated code) must no longer be used.
TYPES = {'Boolean': 'boolean', 'Axis': 'vector2', 'Pose': 'pose', 'Haptic': 'vibration'}
UNREAD = {'VRActionKind.Single': 'vector1', 'VRActionKind.Skeleton': 'skeleton'}
facade = (CORE / 'VRCore/Backends/VRInputActions.cs').read_text(encoding='utf-8-sig')
catalog = {path: (TYPES[kind], requirement.lower())
           for kind, path, requirement in re.findall(r'^\s*(Boolean|Axis|Pose|Haptic)\("([^"]+)", "[^"]*", (\w+), a => (?:\w+) = a\),$', facade, re.M)}
catalog.update({path: (UNREAD[kind], requirement.lower())
                for path, kind, requirement in re.findall(r'^\s*Unread\("([^"]+)", (VRActionKind\.\w+), "[^"]*", (\w+)\),$', facade, re.M)})
manifest = json.loads((ROOT / 'Unity/ValheimVR/Assets/StreamingAssets/SteamVR/actions.json').read_text(encoding='utf-8-sig'))
shipped = {action['name']: (action['type'], action.get('requirement', 'suggested')) for action in manifest['actions']}
for path in sorted(set(catalog) ^ set(shipped)):
    print('only in ' + ('the catalog' if path in catalog else 'actions.json') + ': ' + path)
for path in sorted(set(catalog) & set(shipped)):
    if catalog[path] != shipped[path]:
        print(path + ': ' + str(catalog[path]) + ' in the catalog, ' + str(shipped[path]) + ' in actions.json')
require(catalog == shipped, 'action catalog differs from actions.json')
sets = re.findall(r'new VRActionSetDecl\("([^"]+)", "[^"]*", input => (\w+) = input\.ActionSet\("([^"]+)"\)\)', facade)
require(all(path == lookup for path, _, lookup in sets), 'action set declared under one path and read under another')
require([path for path, _, _ in sets] == [s['name'] for s in manifest['action_sets']], 'action sets differ from actions.json')
assigned = set(re.findall(r'a => (\w+) = a\)', facade)) | {name for _, name, _ in sets}
declared = set(re.findall(r'public static \w+ (\w+) \{ get; private set; \}', facade))
require(declared == assigned, 'action catalog members and their initialization differ')
require(len(assigned) == len(re.findall(r'a => (\w+) = a\)', facade)) + len(sets), 'catalog member assigned twice')
for path in CORE.rglob('*.cs'):
    if not any(part in path.parts for part in ['obj', 'bin']):
        require('SteamVR_Actions.' not in code_only(path.read_text(encoding='utf-8-sig')), 'generated SteamVR_Actions used: ' + str(path.relative_to(ROOT)))
require('SteamVR_Actions' not in (CORE / 'ValheimVRMod.csproj').read_text(encoding='utf-8-sig'), 'SteamVR_Actions still referenced by the project')
require(not (ROOT / 'OpenXR').exists(), 'OpenXR implementation is outside stage 1')
print(f'PASS: provider boundary and {len(catalog)} catalog actions matching actions.json.')
