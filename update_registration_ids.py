"""Run from the CardShop solution root: python update_registration_ids.py
Updates RegisterRequest object initializers in CardShop.IntegrationTests only.
Creates a .bak copy of each changed file. Skips initializers already setting RegistrationId.
"""
from pathlib import Path
import re

root = Path.cwd() / 'CardShop.IntegrationTests'
if not root.is_dir():
    raise SystemExit('Run this script from the CardShop solution root (CardShop.IntegrationTests folder not found).')

pattern = re.compile(r'\bnew\s+RegisterRequest\s*\{')
changed = []
for file in sorted(root.rglob('*.cs')):
    source = file.read_text(encoding='utf-8-sig')
    replacements = []
    for match in pattern.finditer(source):
        brace_start = match.end() - 1
        depth = 0
        end = None
        for index in range(brace_start, len(source)):
            if source[index] == '{':
                depth += 1
            elif source[index] == '}':
                depth -= 1
                if depth == 0:
                    end = index
                    break
        if end is None:
            raise RuntimeError(f'Unclosed RegisterRequest initializer in {file}')
        body = source[brace_start+1:end]
        if re.search(r'\bRegistrationId\s*=', body):
            continue
        newline = '\r\n' if '\r\n' in source else '\n'
        following = source[brace_start+1:]
        indentation_match = re.search(r'(?:\r?\n)([ \t]+)\w+\s*=', following)
        indentation = indentation_match.group(1) if indentation_match else '    '
        insertion = newline + indentation + 'RegistrationId = Guid.NewGuid(),'
        replacements.append((brace_start+1, insertion))
    if replacements:
        updated = source
        for offset, insertion in reversed(replacements):
            updated = updated[:offset] + insertion + updated[offset:]
        backup = file.with_suffix(file.suffix + '.bak')
        if backup.exists():
            raise SystemExit(f'Backup already exists, refusing to overwrite: {backup}')
        backup.write_text(source, encoding='utf-8')
        file.write_text(updated, encoding='utf-8')
        changed.append((file.relative_to(root), len(replacements)))

if changed:
    for name, count in changed:
        print(f'Updated {name}: {count} registration request(s)')
else:
    print('No missing RegistrationId initializers found.')
print('Review the changes before committing. This does not modify unit tests or explicitly configured RegistrationId values.')
