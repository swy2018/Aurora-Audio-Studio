"""Formal-release-first source tracking for packages no longer maintained on PyPI."""
import json
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

PACKAGES = {
    "roformer": ("bs-roformer-infer", "openmirlab/bs-roformer-infer"),
    "mt3": ("mt3-infer", "openmirlab/mt3-infer"),
}


def metadata(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Aurora-Audio-Studio", "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def release(family):
    _, repo = PACKAGES[family]
    base = "https://api.github.com/repos/" + repo
    try:
        version = metadata(base + "/releases/latest")
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        version = None
    if version is None:
        tag = ""
        commit = metadata(base + "/commits?per_page=1")[0]
    else:
        if version.get("draft") or version.get("prerelease"):
            raise ValueError("Expected a formal release")
        tag = version["tag_name"]
        commit = metadata(base + "/commits/" + urllib.parse.quote(tag, safe=""))
    sha = commit["sha"]
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Invalid upstream source revision")
    date = commit["commit"]["committer"]["date"][:10].replace("-", ".")
    return dict(repository="https://github.com/" + repo + ".git", commit=sha, tag=tag, date=date,
                version=tag or date + "+" + sha[:8])


def installed_source(environment):
    receipt = environment / "aurora-runtime.json"
    return json.loads(receipt.read_text()).get("source") if receipt.exists() else None


def check(environment, family):
    target = release(family)
    local = installed_source(environment)
    version = ""
    if local is None:
        package = PACKAGES[family][0].replace("-", "_")
        paths = list(environment.glob("lib/python*/site-packages/" + package + "-*.dist-info/METADATA"))
        if len(paths) == 1:
            version = next((line[9:].strip() for line in paths[0].read_text().splitlines() if line.startswith("Version: ")), "")
            direct = paths[0].with_name("direct_url.json")
            if direct.exists():
                info = json.loads(direct.read_text())
                if info.get("vcs_info", {}).get("vcs") != "git" or info.get("url", "").removesuffix(".git") != target["repository"].removesuffix(".git"):
                    raise ValueError("无法读取已安装版本")
                local = dict(repository=target["repository"], commit=info["vcs_info"].get("commit_id", ""))
    if local is not None:
        if local.get("repository") != target["repository"] or not re.fullmatch(r"[0-9a-f]{40}", local.get("commit", "")):
            raise ValueError("无法读取已安装版本")
        status = "identical" if local["commit"] == target["commit"] else metadata(
            "https://api.github.com/repos/" + PACKAGES[family][1] + "/compare/" + local["commit"] + "..." + target["commit"])["status"]
        if status not in ("ahead", "behind", "identical"):
            raise ValueError("无法读取已安装版本")
        current = status != "ahead"
        newer = status == "behind"
    else:
        formal = target["tag"].lstrip("vV")
        if not re.fullmatch(r"\d+(\.\d+){1,3}", version) or not re.fullmatch(r"\d+(\.\d+){1,3}", formal):
            raise ValueError("无法读取已安装版本")
        numbers = lambda value: tuple(map(int, value.split("."))) + (0,) * (4 - len(value.split(".")))
        current, newer = numbers(version) >= numbers(formal), numbers(version) > numbers(formal)
    unreleased = ""
    if target["tag"]:
        head = metadata("https://api.github.com/repos/" + PACKAGES[family][1] + "/commits?per_page=1")[0]["sha"]
        if not re.fullmatch(r"[0-9a-f]{40}", head):
            raise ValueError("Invalid upstream source revision")
        if head != target["commit"]:
            unreleased = head[:8]
    return dict(available=not current, newer_local=newer, target=target, unreleased=unreleased)
