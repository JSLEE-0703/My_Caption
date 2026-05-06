import argparse
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


def main():
    parser = argparse.ArgumentParser(
        description="Read source text from stdin and translate it with Argos Translate."
    )
    parser.add_argument("--from", dest="source_language", default="en")
    parser.add_argument("--to", dest="target_language", default="zh")
    args = parser.parse_args()

    source_text = sys.stdin.read()
    if not source_text:
        return 0

    source_language = normalize_language(args.source_language, "en")
    target_language = normalize_language(args.target_language, "zh")

    try:
        import argostranslate.translate
    except Exception as ex:
        sys.stderr.write("Argos Translate is not available: {0}\n".format(ex))
        return 1

    try:
        translated_text = argostranslate.translate.translate(
            source_text,
            source_language,
            target_language,
        )
    except Exception as ex:
        sys.stderr.write("Argos translation failed: {0}\n".format(ex))
        return 1

    sys.stdout.write(translated_text or "")
    return 0


if __name__ == "__main__":
    sys.exit(main())
