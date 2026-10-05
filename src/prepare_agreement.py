"""Build-time gender metadata; derived OpenCorpora data, CC BY-SA 3.0.
Usage: python prepare_agreement.py <pymorphy-tool-directory> <Agreement.tsv>
This records dictionary gender, not inferred sentence subjects or gender rewrites.
"""
from collections import defaultdict
import json
from pathlib import Path
import sys
import time

sys.path.insert(0, sys.argv[1])
import pymorphy3

started = time.monotonic()
morph = pymorphy3.MorphAnalyzer()
d = morph.dictionary
slots = {}
for para_id in range(len(d.paradigms)):
    for idx, (_, tag, _) in enumerate(d.build_paradigm_info(para_id)):
        if tag.number != 'sing' or tag.gender not in ('masc', 'femn', 'neut'):
            continue
        if (tag.POS == 'VERB' and tag.tense == 'past') or tag.POS in ('ADJS', 'PRTS'):
            slots[(para_id, idx)] = {'masc': 1, 'femn': 2, 'neut': 4}[tag.gender]

forms = defaultdict(int)
for word, (para_id, idx) in d.words.iteritems():
    mask = slots.get((para_id, idx), 0)
    if mask:
        forms[word] |= mask
        forms[word.replace('ё', 'е')] |= mask

destination = Path(sys.argv[2])
with destination.open('w', encoding='utf-8', newline='\n') as out:
    out.write('# OpenCorpora revision 417150; CC BY-SA 3.0.\n')
    out.write('# word\tgender bitmask: masculine=1 feminine=2 neuter=4\n')
    for word, mask in sorted(forms.items()):
        out.write(f'{word}\t{mask}\n')
report = {'source_revision': d.meta.get('source_revision'), 'gender_forms': len(forms),
          'seconds': round(time.monotonic() - started, 2)}
destination.with_suffix('.meta.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report), flush=True)
