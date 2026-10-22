---
name: agent-detection-source
description: Claude Code session state comes from `claude agents --json`, which the user pointed to; ~/.claude/sessions/*.json was rejected as undocumented
metadata:
  type: reference
---

Claude Code session discovery uses `claude agents --json` (documented at <https://code.claude.com/docs/en/agent-view>): `pid`, `cwd`, `status` (busy / waiting / idle), `waitingFor`. The user pointed to this page on 2026-10-22.

`~/.claude/sessions/<pid>.json` holds similar data (status, cwd) but is not documented, and the docs say files under `~/.claude` are not a stable interface. It was considered and set aside.

Measured on the user's machine: one call takes ~0.25 s and ~0.17 s of CPU, works headless from any folder, and lists VS Code extension sessions. OpenCode has no equivalent, so it is detected by process only.

Related: [[user-parallel-claude-sessions]].
