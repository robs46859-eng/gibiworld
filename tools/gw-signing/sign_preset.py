#!/usr/bin/env python3
"""
Sign a GibiWorld preset asset — GW-ARCH-001 section 6.1.

    python3 tools/gw-signing/sign_preset.py \
        --glb build/randy11/randy11_LOD0.glb \
        --pet-asset-id asset_01J8ZQK5T7VN2MXR4WD6GHYAB3 \
        --species dog --shoulder-height 0.50 \
        --out clients/gw-mobile/Assets/StreamingAssets/presets

GW-ASSET-008: "Preset and Pawsome3D assets use the SAME runtime verifier." A preset is
not a shortcut past verification -- it is an asset whose issuer happens to be
GIBIWORLD_PRESET. It carries a real manifest and a real signature, and the client
validates it exactly as it would a Pawsome3D asset.

Section 6.1 requires:
  * Manifest: canonical JSON, RFC 8785, UTF-8
  * Signature: Ed25519 detached, over the canonical manifest bytes, key ID present
  * Digest: SHA-256 of the GLB

DEV KEYS ONLY. The production signing key never leaves the deployment secret manager
(section 13.1) and never touches a developer machine or this repository.
"""
import argparse
import hashlib
import json
import os
import struct
import sys
from pathlib import Path

try:
    from cryptography.hazmat.primitives.asymmetric.ed25519 import (
        Ed25519PrivateKey, Ed25519PublicKey)
    from cryptography.hazmat.primitives import serialization
except ImportError:
    print("pip install cryptography --break-system-packages", file=sys.stderr)
    raise SystemExit(2)


def canonicalize(obj):
    """
    RFC 8785 JSON Canonicalization Scheme, sufficient subset.

    The manifest contains only strings, integers, floats, booleans, arrays and objects,
    so full JCS number formatting is not exercised. Keys are sorted by UTF-16 code unit,
    separators are minimal, and there is no whitespace. The signature field is EXCLUDED
    by the caller before canonicalization -- signing a document that contains its own
    signature is impossible.
    """
    return json.dumps(obj, sort_keys=True, separators=(',', ':'),
                      ensure_ascii=False).encode('utf-8')


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1 << 20), b''):
            h.update(block)
    return h.hexdigest()


def read_glb_json(path):
    data = Path(path).read_bytes()
    magic, version, _ = struct.unpack_from('<III', data, 0)
    if magic != 0x46546C67 or version != 2:
        raise ValueError('not a glTF 2.0 binary container')
    off = 12
    while off < len(data):
        clen, ctype = struct.unpack_from('<II', data, off)
        off += 8
        if ctype == 0x4E4F534A:
            return json.loads(data[off:off + clen].decode('utf-8'))
        off += clen + (-clen % 4)
    raise ValueError('no JSON chunk')


def measure(gltf):
    """Derive the section 6.2 values the manifest must declare, from the GLB itself."""
    acc = gltf.get('accessors', [])
    tris = verts = 0
    for mesh in gltf.get('meshes', []):
        for prim in mesh.get('primitives', []):
            pos = prim.get('attributes', {}).get('POSITION')
            if pos is not None and pos < len(acc):
                verts += acc[pos].get('count', 0)
            idx = prim.get('indices')
            n = acc[idx].get('count', 0) if idx is not None and idx < len(acc) else 0
            if prim.get('mode', 4) == 4:
                tris += n // 3
    skins = gltf.get('skins', [])
    return {
        'triangles': tris,
        'vertices': verts,
        'materials': len(gltf.get('materials', [])),
        'skinnedMeshes': len(skins),
        'deformBones': max((len(s.get('joints', [])) for s in skins), default=0),
        'clips': [a.get('name', '') for a in gltf.get('animations', [])],
    }


def load_or_create_key(key_path: Path):
    if key_path.exists():
        priv = serialization.load_pem_private_key(key_path.read_bytes(), password=None)
        created = False
    else:
        priv = Ed25519PrivateKey.generate()
        key_path.parent.mkdir(parents=True, exist_ok=True)
        key_path.write_bytes(priv.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()))
        os.chmod(key_path, 0o600)
        created = True
    return priv, created


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--glb', required=True)
    ap.add_argument('--pet-asset-id', required=True)
    ap.add_argument('--species', default='dog')
    ap.add_argument('--shoulder-height', type=float, required=True)
    ap.add_argument('--asset-version', type=int, default=1)
    ap.add_argument('--key-id', default='key_gibi_dev_001')
    ap.add_argument('--key', default='tools/gw-signing/dev-signing-key.pem')
    ap.add_argument('--out', required=True)
    a = ap.parse_args()

    glb = Path(a.glb)
    gltf = read_glb_json(glb)
    m = measure(gltf)

    priv, created = load_or_create_key(Path(a.key))
    if created:
        print(f'generated DEV signing key at {a.key} (gitignored, never production)')

    manifest = {
        'schemaVersion': 1,
        'petAssetId': a.pet_asset_id,
        'assetVersion': a.asset_version,
        'issuer': 'GIBIWORLD_PRESET',
        'keyId': a.key_id,
        'digest': 'sha256:' + sha256_file(glb),
        'species': a.species,
        'skeletonProfile': 'GIBI_QUADRUPED_V1',
        'shoulderHeightM': a.shoulder_height,
        'transferSizeBytes': glb.stat().st_size,
        'materialCount': m['materials'],
        'skinnedMeshCount': m['skinnedMeshes'],
        'deformBoneCount': m['deformBones'],
        'morphTargetCount': 0,
        'lods': [{'level': 0, 'triangles': m['triangles'],
                  'vertices': m['vertices'], 'screenTransition': 1.0}],
        'clips': [{'clipId': c, 'loop': c in ('idle_a', 'walk', 'run'), 'durationS': 2.0}
                  for c in m['clips'] if c],
        'totalKeyframes': 0,
        'bounds': {'minM': [-0.6, -0.4, -0.4], 'maxM': [0.6, 0.4, 0.4]},
        'compatibility': {'minClientSchema': 1, 'maxClientSchema': 1},
        'generatedAt': '2026-08-01T00:00:00Z',
    }

    canonical = canonicalize(manifest)
    signature = priv.sign(canonical)

    signed = dict(manifest)
    signed['signature'] = signature.hex()

    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    stem = a.pet_asset_id
    # Write the manifest in CANONICAL form (sorted keys, no whitespace), not pretty.
    #
    # The signature covers RFC 8785 canonical bytes. A pretty-printed file forces the
    # client to re-canonicalise nested values, and any difference in how it re-emits them
    # -- a space, a newline, a float format -- changes the hash and fails verification.
    # Writing canonical bytes in the first place means the client only has to drop the
    # signature field, never reformat anything it did not produce.
    (out / f'{stem}.manifest.json').write_bytes(
        json.dumps(signed, sort_keys=True, separators=(',', ':'),
                   ensure_ascii=False).encode('utf-8'))
    (out / f'{stem}.glb').write_bytes(glb.read_bytes())

    pub = priv.public_key().public_bytes(
        encoding=serialization.Encoding.Raw,
        format=serialization.PublicFormat.Raw)
    (out / 'trusted-keys.json').write_text(json.dumps(
        {'keys': [{'keyId': a.key_id, 'algorithm': 'Ed25519',
                   'publicKeyHex': pub.hex()}]}, indent=2))

    # Prove the artefact verifies before it ships.
    Ed25519PublicKey.from_public_bytes(pub).verify(signature, canonical)

    print(f'signed  {stem}')
    print(f'  digest      {manifest["digest"][:24]}...')
    print(f'  triangles   {m["triangles"]:,}   materials {m["materials"]}   bones {m["deformBones"]}')
    print(f'  clips       {len(manifest["clips"])}')
    print(f'  signature   {signature.hex()[:32]}... (verified)')
    print(f'  pinned key  {a.key_id} -> {pub.hex()[:32]}...')


if __name__ == '__main__':
    main()
