#!/usr/bin/env python3
"""
Patch the deployed Courier Online page so participant identifiers reach the database.

The page served at /exp/VCOnline/ is Unity's stock WebGL template: it never reads the URL
query string, so window.prolific_pid is unset and PsiturkPlugin.jslib falls back to writing
the literal "UNKNOWN_PID" into every row of `vconline`.

This inserts an identity block immediately before the Unity loader <script>. It is additive --
nothing existing is modified or removed -- idempotent, and it writes a .bak first.

Usage:
    python3 patch_served_index.py <path-to-index.html>
    python3 patch_served_index.py <path> --check     # report only, change nothing
"""

import os
import re
import shutil
import sys

MARKER = "courier-identity-block"
LOADER_RE = re.compile(r'([ \t]*)<script\s+src="Build/VCOnline\.loader\.js"\s*>\s*</script>')

BLOCK = '''<!-- {marker}: sets the identifiers PsiturkPlugin.jslib reads when POSTing to /save.
     Must run before the Unity loader. Keep in sync with deploy/VCOnline_index.html. -->
<script>
  (function () {{
    var p = new URLSearchParams(window.location.search);
    // First param that is present and not an unsubstituted Prolific placeholder
    // (a misconfigured study URL sends the literal "{{{{%PROLIFIC_PID%}}}}").
    var pick = function () {{
      for (var i = 0; i < arguments.length; i++) {{
        var v = p.get(arguments[i]);
        if (v && v.indexOf("{{{{") !== 0) return v;
      }}
      return "";
    }};
    var pid = pick("PROLIFIC_PID", "prolific_pid", "workerId");
    if (!pid) {{
      // sessionStorage lets a mid-task reload keep the same identity.
      pid = sessionStorage.getItem("courier_pid") || ("anon_" + Date.now());
      console.error("Courier: no PROLIFIC_PID in URL; using " + pid);
      window.courierIdentityFallback = true;
    }}
    try {{ sessionStorage.setItem("courier_pid", pid); }} catch (e) {{ /* private mode */ }}
    window.prolific_pid = pid;
    window.study_id     = pick("STUDY_ID", "study_id");
    window.session_id   = pick("SESSION_ID", "session_id");
    console.log("Courier identity:", pid, window.study_id, window.session_id);
  }})();

  // Called by PsiturkPlugin.jslib EndTask() once the final /save completes.
  window.showCourierEndScreen = function (success) {{
    document.body.innerHTML =
      "<div style='color:#fff;background:#000;font-family:sans-serif;font-size:24px;" +
      "text-align:center;padding-top:15%;height:100%;line-height:1.5;'>" +
      (success
        ? "Your data has been saved.<br><br>You are all done \\u2014 you may now close this window and log off."
        : "There was a problem saving your data.<br><br>Please email kahanalab@gmail.com.") +
      "</div>";
  }};
</script>'''.format(marker=MARKER)


def patch(html):
    """Return (new_html, status)."""
    if MARKER in html:
        return html, "already patched"

    m = LOADER_RE.search(html)
    if not m:
        return html, "loader script tag not found"

    indent = m.group(1)
    block = "\n".join(indent + line if line.strip() else line
                      for line in BLOCK.split("\n"))
    return html[:m.start()] + block + "\n\n" + html[m.start():], "patched"


def main(argv):
    if not argv:
        sys.stderr.write(__doc__)
        return 2

    path = argv[0]
    check_only = "--check" in argv[1:]

    with open(path, "r", encoding="utf-8") as f:
        html = f.read()

    new_html, status = patch(html)

    if status == "loader script tag not found":
        sys.stderr.write(
            "ERROR: {}: no <script src=\"Build/VCOnline.loader.js\"></script> found.\n"
            "This may not be the served page. Check with:\n"
            "  grep -n 'loader.js' {}\n".format(path, path))
        return 1

    if status == "already patched":
        print("{}: already contains the identity block; nothing to do.".format(path))
        return 0

    if check_only:
        print("{}: WOULD patch (insert identity block before the Unity loader).".format(path))
        return 0

    backup = path + ".bak"
    if not os.path.exists(backup):
        shutil.copy2(path, backup)
        print("backed up -> {}".format(backup))
    with open(path, "w", encoding="utf-8") as f:
        f.write(new_html)
    print("{}: patched. Reload the study URL and check the console for "
          "'Courier identity: ...'".format(path))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
