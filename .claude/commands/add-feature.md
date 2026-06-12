---
description: Implement a new feature following existing patterns, completely without stopping
---

# Add New Feature (Fully Automated Execution Mode)

**Important:** This workflow is designed to run fully automatically from start to finish, without user intervention. After completing each step, move on to the next step immediately. Do not ask the user for confirmation or interrupt the work mid-thought.

**Argument:** Feature name (e.g., `/add-feature user profile editing`)

---

## Step 1: Preparation and Context Setup

1. Establish the current task context:
  - Feature name: `[feature name given as argument]`
  - Date: `[get the current date in YYYYMMDD format]`
  - Steering directory path: `.steering/[date]-[feature name]/`
2. Create the steering directory above.
3. Create the following 3 empty files:
  - `[steering directory path]/requirements.md`
  - `[steering directory path]/design.md`
  - `[steering directory path]/tasklist.md`

## Step 2: Project Understanding

1. Read `CLAUDE.md` to grasp the overall picture of the project.
2. Review the persistent documents in the `docs/` directory and understand the relevant design philosophy and architecture.

## Step 3: Investigate Existing Patterns

1. Use the Grep tool to search the source code (`src/`) for keywords related to the feature name.
  ```bash
  Grep('[keyword related to the feature]', 'src/')
  ```
2. Analyze the search results and identify existing implementation patterns, naming conventions, and component usage.

## Step 4: Planning Phase (Automatic Generation of Steering Files)

1. Run `Skill('steering')` in **planning mode** and generate the content of the 3 files created in Step 1 (`requirements.md`, `design.md`, `tasklist.md`).
2. **Once this step completes successfully, never stop; proceed immediately to Step 5.**

## Step 5: Implementation Loop (Fully Working Through tasklist.md)

**This step is a loop that automatically repeats until every task in `tasklist.md` is `[x]`.**
**Once this step completes successfully, never stop; proceed immediately to Step 6.**

**Loop start:**

1. Load the task list:
  - Read the `[steering directory path]/tasklist.md` file.

2. Check progress:
  - Check whether any incomplete tasks (`[ ]`) exist in the file.
  - **If no incomplete tasks exist:** consider this implementation loop complete and proceed immediately to **Step 6**.
  - **If incomplete tasks exist:** proceed to the next operation (3. Execute the task).

3. Execute the task:
  - Identify the single **first incomplete task** in `tasklist.md`.
  - Perform the implementation work needed to complete that task.
  - Use `Skill('steering')` in **implementation mode**.
  - Always adhere to the coding conventions of `Skill('development-guidelines')`.

4. Update the task list:
  - When the executed task is complete, use the `Edit` tool to update `tasklist.md`, changing the task from `[ ]` to `[x]`.

5. Continue the loop:
  - **Return to the top of Step 5 (1. Load the task list) and repeat the process.**

---
### ※ Exception Handling Rules Inside the Implementation Loop ※

If any of the following situations occurs while running the implementation loop, handle it autonomously according to these rules and continue the loop.

- **Rule A: The task is too large**
  - **Handling:** Split the current task into multiple smaller subtasks. Use the `Edit` tool to delete the original task and insert the new subtasks (with `[ ]`) in its place. Then continue the loop.

- **Rule B: The task is no longer needed for technical reasons**
  - **Condition:** Applies only when there is a clear technical reason, such as a change in implementation approach, a change in architecture, or a change in dependencies.
  - **Handling:** Use the `Edit` tool to update the task to the format `[x] ~~task name~~ (reason: [briefly state the specific technical reason])`. Then continue the loop.

- **❌ Strictly prohibited actions:**
  - Intentionally skipping an incomplete task for reasons such as "do it later" or "make it a separate task".
  - Ending the loop while leaving incomplete tasks unaddressed without a reason.
  - Asking the user for a decision.

---

## Step 6: Implementation Validation (Launch Subagent)

1. Make a final confirmation that all tasks in `tasklist.md` are complete.
2. Use the `Task` tool to launch the `implementation-validator` subagent and validate quality.
  - `subagent_type`: "implementation-validator"
  - `description`: "Implementation quality validation"
  - `prompt`: "Validate the quality of all changes related to the `[feature name]` implemented in this work. The target files are `[list of paths of implemented files]`. Focus on coding conventions, error handling, testability, and consistency with existing patterns."

**Once this step completes successfully, never stop; proceed immediately to Step 7.**

## Step 7: Run Automated Tests

1. Run the following commands in order and confirm that all tests pass.
  ```bash
  Bash('npm test')
  Bash('npm run lint')
  Bash('npm run typecheck')
  ```
2. If any command produces an error, analyze the problem, generate and apply fix code, then run this step again.

**Once this step completes successfully, never stop; proceed immediately to Step 8.**

## Step 8: Retrospective and Document Updates

1. Run `Skill('steering')` in **retrospective mode** and record handover notes in `tasklist.md`.
  - Implementation completion date
  - Differences between plan and actuals
  - Lessons learned
  - Improvement suggestions for next time

2. Determine whether the current changes affect the project's fundamental design or architecture.

3. If they do, update the relevant persistent documents in `docs/` with the `Edit` tool.

## Completion Criteria

This workflow is automatically considered complete when all of the following conditions are met.
- Step 5: Every task in `tasklist.md` is in a completed state (`[x]` or skipped with a valid reason).
- Step 6: The validation by the `implementation-validator` subagent passes.
- Step 7: All of the `test`, `lint`, and `typecheck` commands succeed without errors.
- Step 8: Handover notes are recorded in `tasklist.md`.

Until these completion criteria are met, continue to think autonomously, solve problems, and keep working.
