import json
import sys

from mdict_utils.reader import MDX, get_record


def build_index(mdx):
    index = {}
    key_list = mdx._key_list

    for position in range(len(key_list)):
        offset, key_bytes = key_list[position]
        try:
            key_text = key_bytes.decode("utf-8")
        except Exception:
            key_text = key_bytes.decode("utf-8", errors="ignore")

        normalized = key_text.strip().lower()
        if not normalized:
            continue

        if normalized not in index:
            index[normalized] = [position]
        else:
            index[normalized].append(position)

    return index


def read_record(mdx, position):
    offset, key = mdx._key_list[position]
    if (position + 1) < len(mdx._key_list):
        length = mdx._key_list[position + 1][0] - offset
    else:
        length = -1

    return get_record(mdx, key, offset, length)


def query_records(mdx, index, text):
    normalized = (text or "").strip().lower()
    if not normalized:
        return ""

    positions = index.get(normalized)
    if not positions:
        return ""

    records = []
    for position in positions:
        record = read_record(mdx, position)
        if record:
            records.append(record)

    return "\n---\n".join(records)


def write_response(response):
    sys.stdout.write(json.dumps(response, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def main():
    if len(sys.argv) < 2:
        write_response({"error": "missing dictionary path"})
        return 1

    dictionary_path = sys.argv[1]
    mdx = MDX(dictionary_path, "")
    index = build_index(mdx)

    for line in sys.stdin:
        payload_text = line.strip()
        if not payload_text:
            continue

        try:
            payload = json.loads(payload_text)
            result_text = query_records(mdx, index, payload.get("text", ""))
            write_response({"result": result_text})
        except Exception as ex:
            write_response({"error": str(ex)})

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
