"""Build time only. No Python or pymorphy is required on the target Windows PC.

pip install --target work/morph-tools pymorphy3==2.0.6 pymorphy3-dicts-ru==2.4.417150.4580142
python prepare_dictionary.py <tool-directory> <Verbs.tsv>
Derived OpenCorpora data: CC BY-SA 3.0. See THIRD-PARTY.txt.
"""
import collections
import json
from pathlib import Path
import sys
import time

sys.path.insert(0, sys.argv[1])
import pymorphy3

started = time.monotonic()
morph = pymorphy3.MorphAnalyzer()
d = morph.dictionary
# Identify paradigms/slots first, so full parsing is unnecessary for millions of noun forms.
slots = {}
for para_id, paradigm in enumerate(d.paradigms):
    info = d.build_paradigm_info(para_id)
    for idx, (_, tag, _) in enumerate(info):
        if tag.POS == 'VERB' and tag.person == '1per' and tag.tense in ('pres', 'futr'):
            target_idx = [j for j, (_, target, _) in enumerate(info)
                          if target.POS == 'VERB' and target.person == '3per'
                          and target.number == tag.number and target.tense == tag.tense]
            slots[(para_id, idx)] = (tag.number, target_idx, info)

candidates = collections.defaultdict(lambda: {'S': set(), 'P': set(), 'unusual': False})
count = 0
for word, (para_id, idx) in d.words.iteritems():
    count += 1
    item = slots.get((para_id, idx))
    if item is None:
        continue
    number, target_idxs, info = item
    stem = d.build_stem(d.paradigms[para_id], idx, word)
    kind = 'S' if number == 'sing' else 'P'
    for target_idx in target_idxs:
        prefix, tag, suffix = info[target_idx]
        if not ({'Infr', 'Erro', 'Slng', 'Arch'} & set(tag.grammemes)):
            candidates[word][kind].add(prefix + stem + suffix)
    if {'Infr', 'Erro', 'Slng', 'Arch'} & set(info[idx][1].grammemes):
        candidates[word]['unusual'] = True

# All е aliases merge before ambiguity checks; never let two readings overwrite one another.
merged = collections.defaultdict(lambda: {'S': set(), 'P': set(), 'unusual': False})
for word, item in candidates.items():
    for key in set((word, word.replace('ё', 'е'))):
        merged[key]['S'].update(item['S'])
        merged[key]['P'].update(item['P'])
        merged[key]['unusual'] |= item['unusual']

safe_count = ambiguous_count = 0
destination = Path(sys.argv[2])
with destination.open('w', encoding='utf-8', newline='\n') as out:
    out.write('# Derived from OpenCorpora, pymorphy3-dicts-ru 2.4.417150.4580142; CC BY-SA 3.0.\n')
    out.write('# word\tnumber(S/P)\tthird-person form(s)\tambiguous(0/1)\n')
    for word, item in sorted(merged.items()):
        parses = [p for p in morph.parse(word) if p.is_known]
        other_reading = any(p.tag.POS != 'VERB' or p.tag.person != '1per' or p.tag.tense not in ('pres', 'futr') for p in parses)
        for kind in ('S', 'P'):
            if not item[kind]:
                continue
            forms = sorted(item[kind])
            # Do not merge ё/e targets: узнаёт (present) and узнает (future)
            # are semantically different despite identical first-person spelling.
            ambiguous = other_reading or item['unusual'] or len(forms) != 1 or bool(item['P' if kind == 'S' else 'S'])
            if ambiguous:
                ambiguous_count += 1
            else:
                safe_count += 1
            out.write('\t'.join((word, kind, ' / '.join(forms), '1' if ambiguous else '0')) + '\n')

report = {'dictionary_version': '2.4.417150.4580142', 'source_revision': d.meta.get('source_revision'),
          'first_person_entries': safe_count + ambiguous_count, 'safe_entries': safe_count,
          'ambiguous_entries': ambiguous_count, 'all_source_records_scanned': count,
          'seconds': round(time.monotonic() - started, 2)}
destination.with_suffix('.meta.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False), flush=True)
