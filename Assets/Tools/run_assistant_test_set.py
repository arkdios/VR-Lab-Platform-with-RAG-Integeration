#!/usr/bin/env python3
"""
Runs the assistant test set against the backend and writes one CSV row for every answer.

The test set comes from the Unity menu "Lab > Export assistant test set". It makes the folder
AssistantTestSet with test_set.jsonl (the payloads) and answers.json (the known fixes, for scoring only).
This script sends only the payloads. It never reads answers.json, so the assistant cannot see a fix.

Each setup is sent at three hint levels and several times, because the answers vary from run to run.
The model, its version and its settings go into the CSV, so every row says what produced it.
The columns diagnosis_correct, action_correct and leaked_fix are left empty for you to score by hand.
Score the diagnosis (what is wrong) and the action (what to do) separately.

Example:
  python Assets/Tools/run_assistant_test_set.py --url http://localhost:8001/ask --runs 5 \
      --model gpt-4o-mini --model-version 2024-07-18 --settings "temperature=0.2" --label with_status

The comparison planned in the 11-20 weekly report (does the scale status help the assistant?) is two runs of the
same setups. The second run removes the status with --drop:
  ... --label with_status
  ... --label without_status --drop spring_scale.connection spring_scale.attached

No extra packages are needed (Python 3.8 or newer).
"""
import argparse
import csv
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

# Columns of the result file. The last four are for the person who scores the answers.
COLUMNS = [
    "label", "config_id", "hint_level", "run", "started_utc", "http_status", "latency_ms",
    "retrieval_ms", "llm_ms", "model", "model_version", "settings", "dropped_fields", "answer",
    "diagnosis_correct", "action_correct", "leaked_fix", "notes",
]


def read_test_set(path, configs, levels):
    """Reads test_set.jsonl and keeps the setups and hint levels that were asked for."""
    entries = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            if configs and entry["config_id"] not in configs:
                continue
            if levels and entry["hint_level"] not in levels:
                continue
            entries.append(entry)
    return entries


def post(url, payload, timeout):
    """Sends one payload. Returns (http status, body text, seconds). Status 0 means the server was not reached."""
    data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read().decode("utf-8", errors="replace")
            return response.status, body, time.perf_counter() - started
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", errors="replace"), time.perf_counter() - started
    except (urllib.error.URLError, TimeoutError, OSError) as error:
        return 0, str(error), time.perf_counter() - started


def drop_fields(payload, paths):
    """Removes fields from the payload, for a controlled comparison. A path is dotted, for example spring_scale.connection."""
    for path in paths:
        parts = path.split(".")
        node = payload
        for part in parts[:-1]:
            node = node.get(part) if isinstance(node, dict) else None
            if node is None:
                break
        if isinstance(node, dict):
            node.pop(parts[-1], None)
    return payload


def read_answer(body):
    """The backend answers {"answer": ..., "retrieval_ms": ..., "llm_ms": ...}. Anything else is kept as text."""
    try:
        parsed = json.loads(body)
        return parsed.get("answer", ""), parsed.get("retrieval_ms", ""), parsed.get("llm_ms", "")
    except (ValueError, AttributeError):
        return body, "", ""


def make_row(args, entry, run, status, body, seconds, started):
    answer, retrieval_ms, llm_ms = read_answer(body)
    return {
        "label": args.label, "config_id": entry["config_id"], "hint_level": entry["hint_level"], "run": run,
        "started_utc": started, "http_status": status, "latency_ms": round(seconds * 1000),
        "retrieval_ms": retrieval_ms, "llm_ms": llm_ms,
        "model": args.model, "model_version": args.model_version, "settings": args.settings,
        "dropped_fields": " ".join(args.drop), "answer": answer, "diagnosis_correct": "", "action_correct": "", "leaked_fix": "", "notes": "",
    }


def run_all(args, entries):
    rows = []
    total = len(entries) * args.runs
    done = 0
    for entry in entries:
        for run in range(1, args.runs + 1):
            payload = drop_fields(json.loads(json.dumps(entry["payload"])), args.drop)
            # A new session and request id for every run, so the backend cannot reuse an earlier answer.
            payload["session_id"] = f"{args.label}-{entry['config_id']}-L{entry['hint_level']}-r{run}"
            payload["request_id"] = done + 1
            started = datetime.now(timezone.utc).isoformat(timespec="seconds")
            status, body, seconds = post(args.url, payload, args.timeout)
            rows.append(make_row(args, entry, run, status, body, seconds, started))
            done += 1
            print(f"[{done}/{total}] {entry['config_id']} level {entry['hint_level']} run {run}: http {status}, {round(seconds * 1000)} ms")
            if args.pause > 0:
                time.sleep(args.pause)
    return rows


def write_csv(path, rows):
    with open(path, "w", encoding="utf-8-sig", newline="") as handle:   # utf-8-sig: Excel opens it right
        writer = csv.DictWriter(handle, fieldnames=COLUMNS)
        writer.writeheader()
        writer.writerows(rows)


def write_run_info(path, args, count):
    """Writes the settings of the run next to the CSV, so the run can be repeated."""
    info = {
        "label": args.label, "url": args.url, "runs_per_setup": args.runs, "rows": count,
        "model": args.model, "model_version": args.model_version, "settings": args.settings,
        "dropped_fields": args.drop, "test_set": os.path.abspath(args.test_set), "levels": sorted(args.levels) if args.levels else "all",
        "configs": sorted(args.configs) if args.configs else "all",
        "finished_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
    }
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(info, handle, indent=2)


def parse_args(argv):
    parser = argparse.ArgumentParser(description="Run the assistant test set against the backend.")
    parser.add_argument("--url", default="http://localhost:8001/ask", help="the backend address (the same as the chat client)")
    parser.add_argument("--test-set", default="AssistantTestSet/test_set.jsonl", help="the payload file from the Unity export")
    parser.add_argument("--runs", type=int, default=5, help="how many times to send each setup and level")
    parser.add_argument("--levels", type=int, nargs="*", default=[], help="hint levels to send, for example 1 2 3. Empty means all")
    parser.add_argument("--configs", nargs="*", default=[], help="setup ids to send, for example A-01 B-03. Empty means all")
    parser.add_argument("--model", required=True, help="the model name, written into every row")
    parser.add_argument("--model-version", default="", help="the model version or date, written into every row")
    parser.add_argument("--settings", default="", help="other settings, for example temperature=0.2;top_k=5")
    parser.add_argument("--drop", nargs="*", default=[], help="payload fields to remove, for a controlled comparison, for example spring_scale.connection spring_scale.attached")
    parser.add_argument("--label", default="run", help="a short name for this run, used in the file names")
    parser.add_argument("--out", default="", help="the result file. Default: AssistantTestSet/results_<label>.csv")
    parser.add_argument("--timeout", type=float, default=60.0, help="seconds to wait for one answer")
    parser.add_argument("--pause", type=float, default=0.0, help="seconds to wait between two requests")
    parser.add_argument("--dry-run", action="store_true", help="count the requests and print the first payload, send nothing")
    args = parser.parse_args(argv)
    args.levels = set(args.levels)
    args.configs = set(args.configs)
    return args


def main(argv):
    args = parse_args(argv)
    if not os.path.exists(args.test_set):
        print(f"Cannot find {args.test_set}. Run the Unity menu Lab > Export assistant test set first.", file=sys.stderr)
        return 2

    entries = read_test_set(args.test_set, args.configs, args.levels)
    if not entries:
        print("No setup matches --configs and --levels.", file=sys.stderr)
        return 2

    print(f"{len(entries)} payloads x {args.runs} runs = {len(entries) * args.runs} requests to {args.url}")
    if args.dry_run:
        print(json.dumps(entries[0]["payload"], indent=2)[:1500])
        return 0

    rows = run_all(args, entries)
    out = args.out or os.path.join(os.path.dirname(args.test_set) or ".", f"results_{args.label}.csv")
    write_csv(out, rows)
    write_run_info(os.path.splitext(out)[0] + ".run.json", args, len(rows))
    failed = sum(1 for row in rows if row["http_status"] != 200)
    print(f"Wrote {len(rows)} rows to {out}" + (f" ({failed} did not answer with http 200)" if failed else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
