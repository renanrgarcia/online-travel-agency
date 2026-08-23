# Code rules

Binding C# conventions for every implementation in this repository — not suggestions, not "when
convenient." Live under `.claude/` — Claude Code's own config directory — rather than under `docs/`,
since this is an instruction to Claude specifically, not project domain spec; referenced from the
repo-root `CLAUDE.md`, which is what's actually auto-loaded every session. See
[`../../docs/specs/tasks/`](../../docs/specs/tasks/README.md) for the task-by-task build spec these
rules apply to.

Each rule names what to do, a short why, and points at the real file in `backend/` that demonstrates it
— read the code, not just the description, when in doubt.

| Rule | Summary |
|---|---|
| [01-generated-regex.md](01-generated-regex.md) | Regex patterns are source-generated (`[GeneratedRegex]`), never `new Regex(...)` |
| [02-primary-constructors.md](02-primary-constructors.md) | Primary constructors for simple dependency capture — no explicit field + constructor body when the parameter is just stored |

Add a rule here the same way a task gets an eval: state it before it's needed broadly, not by
back-filling after inconsistent code already exists.
