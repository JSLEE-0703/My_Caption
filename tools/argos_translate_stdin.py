import argparse
import json
import os
import sys


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ARGOS_ROOT = os.path.join(SCRIPT_DIR, "argos-data")
os.environ.setdefault("XDG_DATA_HOME", ARGOS_ROOT)
os.environ.setdefault("XDG_CONFIG_HOME", ARGOS_ROOT)
os.environ.setdefault("XDG_CACHE_HOME", ARGOS_ROOT)
os.environ.setdefault(
    "ARGOS_PACKAGES_DIR",
    os.path.join(ARGOS_ROOT, "argos-translate", "packages"),
)
os.environ.setdefault("ARGOS_CHUNK_TYPE", "MINISBD")

if hasattr(sys.stdin, "reconfigure"):
    sys.stdin.reconfigure(encoding="utf-8")
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def normalize_language(language, fallback):
    value = (language or "").strip()
    if not value:
        return fallback

    lowered = value.lower()
    if lowered == "auto":
        return "en"

    if lowered in ("zh-cn", "zh-hans", "zh"):
        return "zh"

    if lowered in ("zh-tw", "zh-hant"):
        return "zh"

    if "-" in lowered:
        return lowered.split("-", 1)[0]

    return lowered


def load_translator():
    try:
        import argostranslate.translate
    except Exception as ex:
        sys.stderr.write("Argos Translate is not available: {0}\n".format(ex))
        return None

    return argostranslate.translate


def translate_text(translator, source_text, source_language, target_language):
    try:
        return translator.translate(
            source_text,
            source_language,
            target_language,
        )
    except Exception as ex:
        raise RuntimeError("Argos translation failed: {0}".format(ex))


def run_single(translator, source_language, target_language):
    source_text = sys.stdin.read()
    if not source_text:
        return 0

    try:
        translated_text = translate_text(
            translator,
            source_text,
            source_language,
            target_language,
        )
    except RuntimeError as ex:
        sys.stderr.write(str(ex) + "\n")
        return 1

    sys.stdout.write(translated_text or "")
    return 0


def run_persistent(translator, source_language, target_language):
    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue

        response = {
            "translatedText": "",
            "error": "",
        }

        try:
            payload = json.loads(line)
            source_text = payload.get("text", "")
            response["translatedText"] = translate_text(
                translator,
                source_text,
                source_language,
                target_language,
            ) or ""
        except Exception as ex:
            response["error"] = str(ex)

        sys.stdout.write(json.dumps(response, ensure_ascii=False) + "\n")
        sys.stdout.flush()

    return 0


def main():
    parser = argparse.ArgumentParser(
        description="Read source text from stdin and translate it with Argos Translate."
    )
    parser.add_argument("--from", dest="source_language", default="en")
    parser.add_argument("--to", dest="target_language", default="zh")
    parser.add_argument(
        "--persistent",
        action="store_true",
        help="Keep the process alive and translate one JSON request per stdin line.",
    )
    args = parser.parse_args()

    source_language = normalize_language(args.source_language, "en")
    target_language = normalize_language(args.target_language, "zh")

    translator = load_translator()
    if translator is None:
        return 1

    if args.persistent:
        return run_persistent(translator, source_language, target_language)

    return run_single(translator, source_language, target_language)


if __name__ == "__main__":
    sys.exit(main())
