# Agent Instructions

## Commit Messages

When creating or suggesting commits, use Conventional Commits in English ASCII so GitHub Actions and Discord notifications stay readable.

Format:

```text
<type>(<scope>): <summary>

<body>
```

Rules:
- Use one of these types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `ci`, `build`, `perf`, `revert`.
- Use a short scope when useful, such as `api`, `wip`, `security`, `form`, `deploy`, `ci`, `db`, or `docs`.
- Keep the summary under 72 characters.
- Use imperative mood and be specific about user-visible or API-visible changes.
- Add a body when the change affects frontend/API contracts, deployment behavior, database shape, or migration expectations.
- Do not use vague summaries like `update`, `fix bug`, `change yaml`, or `modify files`.
- Avoid non-ASCII characters in commit messages because Discord notifications may render them incorrectly.

Examples:

```text
feat(security): add nullable user type to registration

Allow clients to send ADM_USER.TYPE during registration.
Blank values are stored as NULL.
```

```text
fix(wip): query ADM_USER for WIP users

Replace UMM_USER lookup with ADM_USER filtered by TYPE=UMM_USER.
```

```text
ci(deploy): send Discord deployment notifications

Notify deployment start and final status using GitHub Actions secrets.
```
