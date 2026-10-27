"""Synchronize authored mechanic flags and guided queues with plans exported by CampaignPacingChecks.

Run the Unity check in Tools/prepare_all_level_playability.py's isolated project,
then use --apply. Default mode verifies assets without writing them.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
FIELDS = ('frostMoves', 'mysteryBuckets', 'linkedBuckets', 'keyGates', 'sectionBuckets',
          'alternatingCrates', 'largeBuckets', 'swappingColors', 'loopInventory')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--output', default='tasks/pacing-validation')
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    assert output.is_relative_to(ROOT / 'tasks'), output
    assert (output / 'status.txt').read_text().startswith('PASS:'), 'Run CampaignPacingChecks successfully first'
    policy = ROOT / 'Assets/Scripts/Data/CampaignProgression.cs'
    expected = json.loads((output / 'source-hashes.json').read_text())[str(policy.relative_to(ROOT)).replace('\\', '/')]
    assert hashlib.sha256(policy.read_bytes()).hexdigest() == expected, 'Policy changed; rerun the Unity check'
    plans = json.loads((output / 'plans.json').read_text(encoding='utf-8'))['levels']
    changed = []
    changed_rules = []
    for plan in plans:
        path = (ROOT / plan['path']).resolve()
        assert path.is_relative_to(ROOT / 'Assets/Levels'), path
        original = path.read_bytes()
        text = original.decode('utf-8-sig')
        revised = text
        for field in FIELDS:
            revised, count = re.subn(r'(^  ' + field + r': )\d+',
                                    lambda m: m[1] + str(int(plan[field])), revised, flags=re.M)
            assert count == 1, (path, field)
        def untouched(value):
            return re.sub(r'^  (' + '|'.join(FIELDS) + r'):.*\n?', '', value, flags=re.M)
        assert untouched(text) == untouched(revised), 'Changed non-mechanic data'
        if text != revised:
            changed.append(plan['path'])
            if args.apply:
                # Keep the existing newline style and BOM.
                data = revised.encode('utf-8')
                if original.startswith(b'\xef\xbb\xbf'):
                    data = b'\xef\xbb\xbf' + data
                path.write_bytes(data)
        rule_path = (ROOT / plan['rulesPath']).resolve()
        assert rule_path.is_relative_to(ROOT / 'Assets/Levels'), rule_path
        original_rule = rule_path.read_bytes()
        rule_text = original_rule.decode('utf-8-sig')
        revised_rule, count = re.subn(r'(^  matchBucketOrderToSand: )\d+',
                                    lambda m: m[1] + str(int(plan['guidedQueue'])), rule_text, flags=re.M)
        assert count <= 1, rule_path
        if count == 0 and plan['guidedQueue']:
            newline = '\r\n' if '\r\n' in rule_text else '\n'
            revised_rule = rule_text.rstrip('\r\n') + newline + '  matchBucketOrderToSand: 1' + newline
        if revised_rule != rule_text:
            changed_rules.append(plan['rulesPath'])
            if args.apply:
                data = revised_rule.encode('utf-8')
                if original_rule.startswith(b'\xef\xbb\xbf'): data = b'\xef\xbb\xbf' + data
                rule_path.write_bytes(data)
    if args.apply:
        journal = output / 'asset-migration.json'
        previous = json.loads(journal.read_text()) if journal.exists() else {}
        changed = sorted(set(changed + previous.get('changed', [])))
        changed_rules = sorted(set(changed_rules + previous.get('changed_rules', [])))
        journal.write_text(json.dumps({
            'levels': len(plans), 'changed': changed, 'changed_rules': changed_rules,
            'preserved': 'All fields except mechanic flags, frostMoves and the guided queue flag; artwork, palettes, level order, seeds and bucket capacities unchanged.',
            'policy_sha256': hashlib.sha256((ROOT / 'Assets/Scripts/Data/CampaignProgression.cs').read_bytes()).hexdigest(),
        }, indent=2), encoding='utf-8')
        print(f'Applied {len(changed)} level flags and {len(changed_rules)} guided queue rules out of {len(plans)} levels.')
    else:
        assert not changed and not changed_rules, f'{len(changed)} levels and {len(changed_rules)} rules need synchronization; run with --apply'
        print(f'PASS: {len(plans)} authored levels match the runtime policy.')


if __name__ == '__main__':
    main()
