# AGENTS.md

## Language and Style

- Reply to the user in Simplified Chinese.
- Write code, comments, commit messages, and documentation in English unless the user explicitly asks otherwise.
- Keep explanations concise but clear.
- Prefer preserving the existing style and structure of the repository.

## Project State

- At the start of a new task, check `PROJECT_STATE.md` if it exists.
- Use `PROJECT_STATE.md` as the first source of project context before inspecting implementation files.
- If `PROJECT_STATE.md` conflicts with the actual repository state, trust the repository state and mention the mismatch to the user.
- Keep `PROJECT_STATE.md` in the repository root.
- After meaningful implementation, debugging, experiment, refactor, or documentation work, update `PROJECT_STATE.md`.
- When updating `PROJECT_STATE.md`, use the `project-state-updater` skill if available.
- Do not update `PROJECT_STATE.md` for trivial explanation-only conversations unless the user asks.

## Default Workflow

Before making any changes:

1. Inspect the relevant repository files and current implementation.
2. Briefly summarize what already exists.
3. Propose a concrete implementation plan.
4. State exactly which files will be created or modified and why.
5. Wait for explicit user approval before editing files.

Do not edit files until the user confirms the plan, unless the user explicitly asks for immediate execution.

## Change Policy

- Make the smallest effective change.
- Avoid broad refactors unless the user explicitly requests them.
- Do not rewrite unrelated code.
- Do not change public APIs, file structure, configs, or tooling unless necessary and approved.
- Preserve existing naming conventions, formatting style, and architecture.
- If a task can be solved by a small local change, prefer that over introducing new abstractions.

## Safety and Environment Rules

- Do not install, remove, or upgrade dependencies without approval.
- Do not change environment variables, shell profiles, PATH, Python environments, Conda environments, Docker settings, CUDA settings, ROS settings, Isaac Sim / Isaac Lab settings, or system tooling without approval.
- Do not create, delete, move, or rename large datasets, model checkpoints, logs, or experiment outputs unless explicitly approved.
- Do not run destructive commands.
- Do not run long training jobs, expensive simulations, or large batch experiments unless the user explicitly approves.
- Prefer read-only inspection commands before proposing changes.

## Handling Unclear Requirements

- If anything important is unclear, ask the user instead of guessing.
- If the user has already given enough information, do not ask unnecessary clarification questions.
- If there are multiple reasonable approaches, briefly compare them and recommend one.
- Do not invent project status, results, metrics, or completed work.

## Before Editing

Before editing, report:

- relevant files inspected
- current implementation summary
- proposed plan
- files to be changed
- why each file needs to change
- risks or assumptions, if any

Then wait for user approval.

## After Editing

After editing, report:

- what changed
- how it works
- why it was changed
- which files were modified
- whether tests, checks, or commands were run
- whether `PROJECT_STATE.md` was updated

If tests were not run, say so clearly.

## Testing and Validation

- Prefer running the smallest relevant test or check.
- Do not claim something is verified unless it was actually run.
- If a command is suggested but not run, mark it as not verified.
- If a test fails, summarize the failure and avoid hiding it.
- If testing is risky, slow, expensive, or requires special hardware, ask before running it.

## Documentation

- Keep documentation factual and concise.
- Update documentation when behavior, setup, workflow, or project status changes.
- Do not include secrets, API keys, tokens, passwords, private credentials, or large logs in documentation.
- For `PROJECT_STATE.md`, record current status, recent changes, known issues, and next steps.

## Git and Commits

- Do not create commits unless the user explicitly asks.
- Do not change branches unless the user explicitly asks.
- Do not reset, rebase, force-push, stash, or discard changes unless explicitly approved.
- Before editing, check whether there are existing user changes and avoid overwriting them.
- If existing uncommitted changes are present, mention them before proceeding.

## Preferred Response Pattern

For implementation tasks, use this pattern:

1. Inspect first.
2. Summarize current state.
3. Propose plan.
4. Wait for approval.
5. Modify only approved files.
6. Run approved or relevant checks.
7. Report results.
8. Update `PROJECT_STATE.md` when appropriate.

## Strict Rule

Do not make code or file changes before explicit user approval, unless the user clearly says to proceed immediately.