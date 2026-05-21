# WorkItemsMCP

A **Model Context Protocol (MCP) server** for persistent, markdown-based task tracking. Designed to let an AI agent resume each day, read work item files, update tasks based on conversation, and maintain daily logs — all stored as plain markdown files in a local vault.

---

## Quick start

### 1. Set the vault environment variable

```bash
export TASK_TRACKER_VAULT=/path/to/your/task-vault
```

### 2. Run the server (stdio transport)

```bash
dotnet run --project src/WorkItemsMcp
```

### 3. Register with your MCP client (e.g. Claude Desktop)

```json
{
  "mcpServers": {
    "work-items": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/WorkItemsMCP/src/WorkItemsMcp"],
      "env": {
        "TASK_TRACKER_VAULT": "/path/to/your/task-vault"
      }
    }
  }
}
```

---

## Vault structure

```
task-vault/
  .task-tracker/
    index.json        ← task metadata index
    next-id.txt       ← auto-increment ID counter
    config.json       ← vault config
  tasks/
    TASK-0001.md      ← one file per task (YAML front matter + markdown)
    TASK-0002.md
  daily/
    2026-05-21.md     ← one daily note per day
  views/
    active.md         ← auto-generated view: not-started + in-progress
    urgent.md         ← auto-generated view: high + urgent urgency
    blocked.md        ← auto-generated view: blocked tasks
    completed.md      ← auto-generated view: done + cancelled
```

---

## MCP Tools

### Vault management

| Tool | Description |
|------|-------------|
| `get_vault_status` | Checks whether `TASK_TRACKER_VAULT` is set and the vault is initialized. Returns `setup_required`, `not_initialized`, or `ready`. |
| `initialize_vault` | Creates the vault folder structure at the given path (`.task-tracker/`, `tasks/`, `daily/`, `views/`). Safe to call on an existing vault — never overwrites existing files. |

### Daily workflow

| Tool | Description |
|------|-------------|
| `resume_day` | Creates or loads today's daily note. Returns lists of active, urgent, and blocked tasks — the agent's starting context for the day. |
| `append_daily_update` | Adds a timestamped update entry to the daily note, optionally linking related task IDs. |

### Task management

| Tool | Description |
|------|-------------|
| `create_task` | Creates a new task file with auto-generated ID (`TASK-NNNN`). Appends a new-task entry to the daily note and rebuilds views. |
| `get_task` | Reads one task by ID. Returns metadata and full markdown content. |
| `update_task` | Updates task metadata (status, urgency, blocked, deadline) and/or content sections (description, next actions, open questions). Appends a history entry and rebuilds views. |
| `append_task_update` | Adds a history note to a task without touching any structured fields. Useful for logging observations or agent notes. |
| `list_tasks` | Lists tasks, optionally filtered by `status`, `urgency`, or `blocked` state. |
| `search_tasks` | Full-text search across all task markdown files. Returns matching task IDs, titles, and content snippets. |

### Views

| Tool | Description |
|------|-------------|
| `rebuild_views` | Regenerates all four view files (`active.md`, `urgent.md`, `blocked.md`, `completed.md`) from current task metadata. |

---

## Task format

Each task is a markdown file with YAML front matter:

```markdown
---
id: TASK-0001
title: "Fix restore cancellation loop"
status: in-progress
urgency: high
created: 2026-05-21
updated: 2026-05-21
deadline:
blocked: false
---

# TASK-0001 - Fix restore cancellation loop

## Description
...

## Current state
Status: In Progress
Urgency: High
Blocked: No

## Next actions
- [ ] Review customer report
- [ ] Identify restore/design-time build loop

## Open questions
- Does this reproduce only with gRPC projects?

## History

### 2026-05-21
Created task.
```

### Valid status values

`not-started` · `in-progress` · `blocked` · `done` · `cancelled`

### Valid urgency values

`low` · `normal` · `high` · `urgent`

---

## Agent workflow

```
1. Agent calls get_vault_status
2. If setup_required → agent asks user where to store the vault
3. Agent calls initialize_vault with chosen path
4. Agent calls resume_day with today's date
5. Agent summarises active / urgent / blocked work to the user
6. User gives updates
7. Agent calls create_task, update_task, or append_task_update as needed
8. Agent calls append_daily_update to log the conversation summary
9. Agent calls rebuild_views
10. Agent summarises what changed
```

---

## Error responses

All tools return structured JSON. On error:

```json
{
  "status": "error",
  "errorCode": "task_not_found",
  "message": "Task TASK-9999 was not found."
}
```

Error codes: `vault_not_configured` · `vault_not_initialized` · `task_not_found` · `invalid_status` · `invalid_urgency` · `invalid_date` · `io_error`

---

## Project structure

```
src/WorkItemsMcp/
  Program.cs
  Tools/
    VaultTools.cs       ← get_vault_status, initialize_vault
    TaskTools.cs        ← create_task, get_task, update_task, append_task_update, list_tasks, search_tasks
    DailyTools.cs       ← resume_day, append_daily_update
    ViewTools.cs        ← rebuild_views
  Services/
    VaultService.cs
    TaskRepository.cs
    DailyNoteService.cs
    ViewBuilder.cs
    MarkdownTaskSerializer.cs
    TaskIdGenerator.cs
  Models/
    TaskItem.cs
    VaultTypes.cs
```

## Requirements

- .NET 9
- `ModelContextProtocol` NuGet package (official Microsoft SDK)
- `YamlDotNet` NuGet package