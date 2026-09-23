# Free Local AI Dev Team

A free, local-first AI software team with a live dashboard. Assign locally installed Ollama models to eight roles, watch their status, and inspect the prompts and answers passed between agents.

## Principles

- No paid model APIs and no API keys. The dashboard accepts only a loopback Ollama endpoint; it refuses remote Ollama hosts.
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

## Run the dashboard

```powershell
dotnet run --project src/FreeLocalAiDevTeam.csproj
```

Open [http://127.0.0.1:5080](http://127.0.0.1:5080). The dashboard lists the models installed in Ollama and lets you assign a model to each role. Add a project brief and start the team. You can stop a run at any time.

The dashboard shows which agent is running, each prompt and response, and the model used. The conversation transcript is saved as `work/<run-id>/conversations.jsonl`; role outputs are saved as Markdown files in the same folder. The dashboard restores the latest transcript when restarted. Keep the terminal window open while the dashboard runs; press `Ctrl+C` there to stop the local server.

For a direct command-line run, provide a brief:

```powershell
dotnet run --project src/FreeLocalAiDevTeam.csproj -- "Build a small inventory dashboard for independent bookstores"
```

No prompt or project files are sent to a cloud AI service. Install additional free local models with Ollama, then select them per role in the dashboard. The default model endpoint is `http://127.0.0.1:11434`.

## Current scope

This starter creates a reviewable project blueprint. Direct code editing, autonomous Git operations, web research, and GitHub publishing are intentionally not included yet. Each should be added behind explicit permissions and human review.

## License

MIT. See [LICENSE](LICENSE).
