import re
from pathlib import Path


def _definition(root_dir, name):
    pattern = re.compile(
        r'^\s*(?:(?:internal|public|private|static|sealed|abstract)\s+)*'
        r'(?:class|struct|interface|enum)\s+' + re.escape(name) + r'\b', re.M)
    for path in sorted((Path(root_dir) / 'src').rglob('*.cs')):
        text = path.read_text(encoding='utf-8-sig')
        match = pattern.search(text)
        if match:
            return path, text, match
    raise KeyError(name)


def file_path(root_dir, name):
    return _definition(root_dir, name)[0]


def file_text(root_dir, name):
    return _definition(root_dir, name)[1]


def class_text(root_dir, name):
    _, text, match = _definition(root_dir, name)
    depth, index = 0, text.index('{', match.end())
    while True:
        if text[index] == '{':
            depth += 1
        elif text[index] == '}':
            depth -= 1
            if depth == 0:
                return text[match.start():index + 1]
        index += 1


def without_comments(text):
    return re.sub(r'(?m)^[ \t]*//[^\n]*\n', '', text)


def class_code(root_dir, name):
    return without_comments(class_text(root_dir, name))


def file_code(root_dir, name):
    return without_comments(file_text(root_dir, name))
