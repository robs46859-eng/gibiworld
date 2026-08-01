---
name: gibi-spec-gate
description: Run every GibiWorld compliance gate and report pass/fail against GW-ARCH-001. Use before committing, before a release, when asked "does this still comply", "run the gates", "is the build green", or after changing anything under clients/gw-mobile, contracts/, or db/migrations.
---

# GibiWorld spec gate

Runs the checks that GW-ARCH-001 §17 makes normative. **A missing result is a failed
release gate** (§19) — never report a skipped check as a pass.

## Order

Run cheapest-first so a contract break is caught in seconds rather than behind a
five-minute Unity boot.

### 1. Contracts (no Unity needed)

```bash
cd /Users/robert/gibiworld
python3 -c "
import json,glob,sys
from jsonschema import Draft202012Validator as V
bad=0
for f in glob.glob('contracts/schemas/*.json'):
    try: V.check_schema(json.load(open(f))); print('OK  ',f)
    except Exception as e: print('FAIL',f,e); bad=1
sys.exit(bad)"
python3 -c "
import yaml,sys
d=yaml.safe_load(open('contracts/openapi/gibiworld.v1.yaml'))
assert d['openapi'].startswith('3.1'), 'section 11 requires OpenAPI 3.1'
n=len(d['paths']); print('paths:',n)
sys.exit(0 if n>=13 else 1)"
```

§11 tabulates **13 endpoints**. Fewer means one was dropped — that is a contract
regression, not a cosmetic diff.

### 2. Assembly layering (no Unity needed)

```bash
python3 tools/check_assembly_refs.py
```

Catches provider-SDK leaks, §4 layering violations, and asmdef references that do not
cover what the source imports. This last one is why the checker exists: `ARFoundation`
and `ARSubsystems` are **separate assemblies**, and collapsing them makes the check pass
vacuously.

### 3. Unity gates

Always run **headless**. The editor console serves cached results and will lie about
whether a fix took.

```bash
U=/Applications/Unity/Hub/Editor/6000.0.74f1/Unity.app/Contents/MacOS/Unity
P=/Users/robert/gibiworld/clients/gw-mobile

$U -batchmode -quit -nographics -projectPath "$P" \
   -executeMethod Gibi.Editor.AssemblyGraphCheck.CheckForCI -logFile /tmp/g1.log
$U -batchmode -quit -nographics -projectPath "$P" \
   -executeMethod Gibi.Editor.SceneValidator.ValidateAllForCI -logFile /tmp/g2.log
$U -batchmode -nographics -projectPath "$P" -runTests -testPlatform EditMode \
   -testResults /tmp/results.xml -logFile /tmp/g3.log
```

Read `/tmp/results.xml`, not the log:

```bash
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('/tmp/results.xml').getroot()
print(f\"total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}\")
for tc in r.iter('test-case'):
    if tc.get('result')!='Passed': print('FAIL:',tc.get('fullname'))"
```

### 4. Database invariants (needs a PostGIS container)

`course_versions` must **reject** an UPDATE (GW-GAME-007). If the statement succeeds,
immutability is broken and the build fails.

## Reading failures

- **Test failure** — check whether the test or the implementation is wrong. Precedent:
  GW-GAME-002 once failed because the *test* called `Consume()` inside a loop condition,
  manufacturing the divergence it was written to detect.
- **Unity reports an error whose line numbers do not match the file on disk** — stale log
  or cached compile. Verify against `Library/ScriptAssemblies/*.dll` mtimes and re-run
  headless.
- **Never** report a gate as passing because it was not run.

## Baseline

As of 2026-08-01: compile clean, assembly graph clean, GW-AR-001 passing, **27/27**
EditMode tests. Any regression from that is new.
