# Free Local AI Dev Team

A free, local-first AI software team that turns a project brief into research notes, requirements, architecture, implementation tasks, review findings, QA guidance, DevOps guidance, and launch copy.

## Principles

- No paid model APIs and no API keys. Model requests go only to a local Ollama server.
- Agent outputs are advisory; review them before running or publishing.
- The first version creates planning artifacts and does not edit or push to a target repository.
- Future coding must use a separate Git branch and require human approval before push, merge, or deploy.

## Agent team

1. Idea researcher
2. Product analyst
3. Software architect
4. Implementer
5. Independent reviewer
6. QA engineer
7. DevOps engineer
8. Marketing writer

## Requirements

- .NET 10 SDK
- Ollama installed and running locally
- At least one model already downloaded

The app discovers installed models from `http://localhost:11434/api/tags`; it does not download models automatically.

## Run

```powershell
dotnet run --project src/FreeLocalAiDevTeam.csproj -- "Build an inventory dashboard for independent bookstores"
```

The workflow writes Markdown outputs under `work/<run-id>/`. No project prompt is sent to a cloud AI provider.

## Current scope

This starter creates a reviewable project blueprint. Autonomous code editing, web research, and GitHub publishing are not included yet.

## License

MIT. See [LICENSE](LICENSE).
