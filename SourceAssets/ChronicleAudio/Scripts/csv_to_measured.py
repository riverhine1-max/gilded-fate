"""Convert a manually verified timing sheet; never fill missing timestamps."""
import argparse
import csv
import json
import math
from pathlib import Path


def convert(path):
    groups = {}
    with path.open(encoding='utf-8', newline='') as stream:
        for row in csv.DictReader(stream):
            groups.setdefault(row['lineId'], []).append(row)
    measured = {}
    for line_id, rows in groups.items():
        if all(not row['startSeconds'].strip() and not row['endSeconds'].strip() for row in rows):
            continue
        previous = 0.0
        words = []
        for number, row in enumerate(rows):
            if int(row['wordIndex']) != number:
                raise ValueError(line_id + ': missing or reordered word rows')
            if row['verifiedByListening'].strip().lower() != 'true':
                raise ValueError(line_id + ': each measured interval needs listening verification')
            start = float(row['startSeconds'])
            end = float(row['endSeconds'])
            if not (math.isfinite(start) and math.isfinite(end) and 0 <= previous <= start < end):
                raise ValueError(line_id + ': invalid interval')
            words.append({'text': row['text'], 'startSeconds': start, 'endSeconds': end})
            previous = end
        measured[line_id] = {'words': words, 'verifiedByListening': True}
    if not measured:
        raise ValueError('No real verified timings supplied; blank templates are not production data.')
    return measured


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('sheet', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.write_text(json.dumps(convert(args.sheet), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
