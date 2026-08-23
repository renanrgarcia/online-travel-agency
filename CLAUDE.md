# CLAUDE.md — online-travel-agency

Project-scoped instructions. Takes precedence over `~/.claude/CLAUDE.md` where the two overlap, per
that file's own stated precedence order.

## Code rules — binding, not optional

Before writing or editing any C# in `backend/`, follow every rule in
[`.claude/rules/`](.claude/rules/README.md). They exist specifically to catch patterns that compile
fine but aren't how this codebase is written. When in doubt, read the reference implementation each
rule links to.

## Review workflow

Implement, then stop. Do not narrate the implementation, walk through the diff, or explain design
decisions until the user has reviewed and validated the code themselves — they review by editing the
files directly, not from a live diff in chat. Do not `git commit` until they've confirmed the code is
validated. If they make a change after you've already commented on something, treat your comment as
withdrawn and wait to be asked again rather than re-asserting it.

## Spec

Everything else — architecture, the task-by-task build order, deployment — lives under
[`docs/`](docs/README.md). Start there for context on any task.
