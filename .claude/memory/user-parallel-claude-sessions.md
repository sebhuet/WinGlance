---
name: user-parallel-claude-sessions
description: The user runs many Claude Code sessions at once, one per VS Code window — the main use case behind WinGlance's agent badges
metadata:
  type: user
---

The user works with several Claude Code sessions in parallel, each in its own VS Code window (five at once on 2026-10-22, through the VS Code extension), and also uses OpenCode in terminals. They asked for WinGlance to show, per window, whether an agent is still running.

**Why:** WinGlance is their own tool for keeping an eye on these windows; the agent badges (FR-11) exist for this workflow.

**How to apply:** when changing window tracking or the Preview view, check the VS Code multi-window case first: one `Code.exe` process owns every window, so windows are told apart by the folder in their title. See [[agent-detection-source]].
