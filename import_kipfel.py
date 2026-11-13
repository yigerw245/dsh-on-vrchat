from collections import deque
import argparse
from pathlib import Path, PurePosixPath
import re
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parent
PROJECT = ROOT / 'unity'
ASSETS = PROJECT / 'Assets' / 'DafeiyuKipfel'
SOURCE_SCENE = 'Assets/deep.unity'
SCENE_PARTS = re.compile(rb'(?m)^--- !u!(\d+) &(-?\d+)( stripped)?\r?$')
GUID = re.compile(rb'guid: ([a-f0-9]{32})')
VISUAL_CLASSES = {b'1', b'4', b'23', b'33', b'137', b'1001', b'1660057539'}
SUPPORTED_ASSETS = {'.prefab', '.fbx', '.asset', '.mat', '.png', '.jpg', '.jpeg', '.psd', '.tga', '.exr', '.tif', '.tiff', '.hdr', '.dds'}
SKIP_PREFABS = (
    'Assets/LightLimitChanger/',
    'Assets/汐音汉化/ABT/',
    'Assets/MOCHIYAMA/Kipfel/Prefab/AvatarDynamics.prefab',
    'Packages/vrchat.blackstartx.gesture-manager/',
)


def sanitize(data, index, source_scene):
    markers = list(SCENE_PARTS.finditer(data))
    blocks = []
    removed = set()
    skipped_instances = set()
    for pos, match in enumerate(markers):
        end = markers[pos + 1].start() if pos + 1 < len(markers) else len(data)
        block = data[match.start():end]
        blocks.append((match[1], match[2], block))
        if match[1] not in VISUAL_CLASSES:
            removed.add(match[2])
        if source_scene and match[1] == b'1001':
            prefab = re.search(rb'm_SourcePrefab: \{fileID: [^,]+, guid: ([a-f0-9]+)', block)
            if prefab and index.get(prefab[1], '').startswith(SKIP_PREFABS):
                skipped_instances.add(match[2])

    removed.update(skipped_instances)
    for _, file_id, block in blocks:
        instance = re.search(rb'(?m)^  m_PrefabInstance: \{fileID: (-?\d+)\}', block)
        if instance and instance[1] in skipped_instances:
            removed.add(file_id)

    result = [data[:markers[0].start()]]
    for kind, file_id, block in blocks:
        if file_id in removed:
            continue
        if kind == b'1':
            block = re.sub(rb'(?m)^  - component: \{fileID: (-?\d+)\}\r?\n',
                           lambda m: b'' if m[1] in removed else m[0], block)
        if kind == b'4':
            match = re.search(rb'(?m)^  m_Children:\r?\n((?:  - \{fileID: -?\d+\}\r?\n)+)', block)
            if match:
                children = re.sub(rb'(?m)^  - \{fileID: (-?\d+)\}\r?\n',
                                  lambda m: b'' if m[1] in removed else m[0], match[1])
                block = block[:match.start()] + (b'  m_Children:\n' + children if children else b'  m_Children: []\n') + block[match.end():]
        if kind == b'1001':
            block = re.sub(rb'(?ms)^    m_AddedComponents:.*?(?=^  m_SourcePrefab:)', b'    m_AddedComponents: []\n', block)
        if source_scene and kind == b'1660057539':
            block = re.sub(rb'(?ms)(^  m_Roots:).*', rb'\1\n  - {fileID: 3902425463769900396}\n', block)
        result.append(block)
    return b''.join(result)


def write_asset(path, data):
    target = path.resolve()
    if not target.is_relative_to(ASSETS.resolve()):
        raise ValueError(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description='将已有的大肥鱼 Unity 源项目 ZIP 导入本地 Demo。')
    parser.add_argument('archive', nargs='?', type=Path, default=ROOT / '大肥鱼kipfel.zip')
    archive_path = parser.parse_args().archive
    if not archive_path.is_file():
        raise SystemExit('请把你有权使用的 大肥鱼kipfel.zip 放到仓库根目录。')
    with ZipFile(archive_path) as archive:
        names = set(archive.namelist())
        if SOURCE_SCENE not in names:
            raise SystemExit(f'压缩包内找不到 {SOURCE_SCENE}。这个导入器需要大肥鱼的 Unity 源项目 ZIP。')
        index = {}
        for name in names:
            if name.endswith('.meta'):
                guid = re.search(rb'(?m)^guid: ([a-f0-9]{32})', archive.read(name))
                if guid:
                    index[guid[1]] = name[:-5]

        pending = deque([SOURCE_SCENE])
        seen = set()
        while pending:
            name = pending.popleft()
            if name in seen:
                continue
            seen.add(name)
            data = archive.read(name)
            suffix = PurePosixPath(name).suffix.lower()
            if suffix in {'.prefab', '.unity'}:
                data = sanitize(data, index, name == SOURCE_SCENE)
            relative = PurePosixPath(name).relative_to('Assets')
            if '..' in relative.parts:
                raise ValueError(f'Invalid asset path: {name}')
            target = ASSETS.joinpath(*relative.parts)
            write_asset(target, data)
            meta = name + '.meta'
            if meta in names:
                write_asset(target.with_name(target.name + '.meta'), archive.read(meta))
            for payload in [data, archive.read(meta) if meta in names else b'']:
                for guid in set(GUID.findall(payload)):
                    dependency = index.get(guid)
                    if dependency and dependency in names and dependency.startswith('Assets/'):
                        if PurePosixPath(dependency).suffix.lower() in SUPPORTED_ASSETS:
                            pending.append(dependency)
        print(f'Imported {len(seen)} model assets. Install lilToon in Creator Companion before opening the model.')


if __name__ == '__main__':
    main()
