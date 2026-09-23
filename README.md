# Free Local AI Dev Team

A free, local-first AI software team that turns a project brief into research notes, requirements, architecture, implementation tasks, review findings, QA guidance, DevOps guidance, and launch copy.

## Principles

- No paid model APIs and no API keys. Model requests go only to a local Ollama server.
- The agent roles are advisory. Review generated changes before running or publishing them.
- The first version writes planning artifacts; it does not edit a target repository or push to GitHub.
- A future coding mode must use a separate Git branch, inspect the diff, and require human approval before push, merge, or deploy.

## Agent team

1. **Idea researcher** — explores the brief, identifies a target user and differentiators, and proposes validation questions.
2. **Analyst** — writes problem statement, user stories, and acceptance criteria.
3. **Architect** — proposes components, data flow, and technical decisions.
4. **Implementer** — breaks the scope into small, reviewable implementation tasks.
5. **Independent reviewer** — looks for security, correctness, maintainability, and scope risks.
6. **QA engineer** — recommends test cases and a manual verification plan.
7. **DevOps engineer** — proposes local development, CI, packaging, and deployment steps.
8. **Marketing writer** — drafts a README summary, value proposition, and launch copy.

## Requirements

- .NET 10 SDK
- Ollama installed and running locally
- At least one Ollama model already downloaded

The app discovers locally installed models from `http://localhost:11434/api/tags`. It does not download a model automatically.

## Run

```powershell
dotnet run --project src/FreeLocalAiDevTeam.csproj -- "Build a small inventory dashboard for independent bookstores"
```

The workflow saves Markdown files under `work/<run-id>/`. No prompt or project files are uploaded to a cloud AI service.

## Current scope

This starter creates a reviewable project blueprint. Direct code editing, autonomous Git operations, web research, and GitHub publishing are intentionally not included yet. Each should be added behind explicit permissions and human review.

## License

MIT. See [LICENSE](LICENSE).
