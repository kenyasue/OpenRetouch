---
name: steering
description: A skill for recording the work plan and task list for each work instruction in documents. Load it during work planning triggered by user instructions, during implementation, and during verification.
allowed-tools: Read, Write
---

# Steering Skill

A skill that supports implementation based on steering files (`.steering/`) and ensures reliable progress management of tasklist.md.

## Purpose of This Skill

- Support creation of steering files (requirements.md, design.md, tasklist.md)
- Manage incremental implementation based on tasklist.md
- **Automatically track progress and enforce tasklist.md updates**
- Record a retrospective after implementation is complete

## When to Use

Use this skill at the following times:

1. **Work planning**: When creating steering files
2. **Implementation**: When implementing according to tasklist.md
3. **Verification**: When recording a retrospective after implementation is complete

## Mode 1: Creating Steering Files

### Purpose
Create steering files for a new feature or change.

### Steps

1. **Check the steering directory**
   ```
   Get the current date and create a directory in the format `.steering/[YYYYMMDD]-[feature-name]/`
   ```

2. **Review the persistent documents**
   - `docs/product-requirements.md`
   - `docs/functional-design.md`
   - `docs/architecture.md`
   - `docs/repository-structure.md`
   - `docs/development-guidelines.md`

   Read these to understand the project's direction.

3. **Create files from templates**

   Read the following templates, replace the placeholders with concrete content, and create the files:

   - `.claude/skills/steering/templates/requirements.md` → `.steering/[date]-[feature-name]/requirements.md`
   - `.claude/skills/steering/templates/design.md` → `.steering/[date]-[feature-name]/design.md`
   - `.claude/skills/steering/templates/tasklist.md` → `.steering/[date]-[feature-name]/tasklist.md`

4. **Refine tasklist.md**

   Based on requirements.md and design.md, refine tasklist.md:
   - Describe the tasks for each phase concretely
   - Make subtasks explicit as well
   - Specify the order of implementation

## Mode 2: Implementation (Most Important)

### Purpose
Proceed with implementation according to tasklist.md and **reliably record progress in the document**.

### 🚨 Critical Principles

**MUST (required)**:
- Keep tasklist.md open at all times while implementing
- When starting a task, always use the Edit tool to update `[ ]`→`[x]`
- When completing a task, always use the Edit tool to record completion
- **Continue working until all tasks in tasklist.md are complete**
- NEVER: do not move on to the next task without updating tasklist.md

**NEVER (prohibited)**:
- Proceeding with implementation without looking at tasklist.md
- Managing progress with the TodoWrite tool alone (TodoWrite is auxiliary; tasklist.md is the official record)
- Updating multiple tasks in a batch (update in real time)
- **Skipping tasks for reasons such as "due to time constraints" or "planned as a separate task"**
- **Ending work while incomplete tasks (`[ ]`) remain**

### 🚨 Principle of Complete Task Completion

**Rules that must absolutely be followed**:

1. **Continue working until all tasks in tasklist.md are complete**
   - Keep implementing until every task is `[x]`
   - Do not skip tasks for reasons such as "it takes too long" or "it's difficult"
   - Do not write the retrospective while incomplete tasks remain

2. **Skipping tasks is prohibited in principle**
   - "Planned as a separate task due to time constraints" is prohibited
   - "Deferred because the implementation is too complex" is prohibited
   - Reasons such as "it's hard, so later" or "testing is tedious" are prohibited
   - Skipping is only permitted for technical reasons (see below)

3. **What to do when a task is too large**
   - Split the task into smaller subtasks
   - Add the split subtasks to tasklist.md
   - Complete the subtasks one at a time

4. **Skipping is permitted only when a task has become unnecessary for technical reasons**

   Skipping is allowed only when one of the following technical reasons applies:
   - A change in implementation approach made the feature itself unnecessary
   - An architecture change replaced it with a different implementation method
   - A dependency change made the task impossible to execute
   - A higher-level design change made this task meaningless

   Skip procedure:
   - Note the technical reason in tasklist.md and mark the task as skipped
   - Example: `- [x] ~~Task name~~ (No longer needed due to implementation approach change: the architecture was changed from X to Y, making this layer unnecessary)`
   - Record the reason for the change in detail in the retrospective section

5. **Examples of what NOT to do when incomplete tasks remain**
   ```markdown
   ## Post-Implementation Retrospective
   **Tasks not implemented**:
   - Test implementation (planned as a separate task due to time constraints) ❌ Absolutely unacceptable
   ```

6. **The correct form of completion**
   - All tasks are `[x]`
   - The retrospective section contains no "tasks not implemented" entries
   - If the implementation approach changed, the reason is clearly documented

### Implementation Flow

#### Step 1: Read tasklist.md

```
Read('.steering/[date]-[feature-name]/tasklist.md')
```

Grasp the overall task structure and identify the next task to work on.

#### Step 2: Start task management with TodoWrite

Create a task list with the TodoWrite tool based on the contents of tasklist.md:
- This is an auxiliary internal note for Claude Code
- **tasklist.md is the official document**

#### Step 3: Task loop (repeat for each task)

**3-1. Check the next task**
```
Read tasklist.md and identify the next incomplete task (`[ ]`)
```

**3-2. Record the task start in tasklist.md (required)**
```
Use the Edit tool to update the corresponding line in tasklist.md from `[ ]` to `[x]`

Example:
old_string: "- [ ] Implement StorageService"
new_string: "- [x] Implement StorageService"
```

**Important**: Immediately after running the Edit tool, confirm that the update succeeded.

**3-3. Update the status in TodoWrite as well**
```
Use the TodoWrite tool to change the corresponding task to "in_progress"
```

**3-4. Perform the implementation**
```
Implement according to the development guidelines (docs/development-guidelines.md)
```

**3-5. Record the task completion in tasklist.md (required)**
```
After implementation is complete, always use the Edit tool to update tasklist.md and record completion

If there are subtasks, update each subtask individually as well
```

**3-6. Update the status in TodoWrite as well**
```
Use the TodoWrite tool to change the corresponding task to "completed"
```

**3-7. Move on to the next task**
```
Return to step 3-1
```

#### Step 4: Verification at phase completion

When each phase (e.g., Phase 1, Phase 2) is complete:

1. **Read tasklist.md and check progress**
   ```
   Read('.steering/[date]-[feature-name]/tasklist.md')
   ```

2. **Verify the completed tasks**
   - Are all tasks `[x]`?
   - Are there any overlooked tasks?

3. **Report to the user**
   ```
   "Phase 1 is complete. Please check the progress in tasklist.md."
   ```

#### Step 4.5: All-tasks-complete check (required)

**After all phases are implemented, always run this before writing the retrospective**:

1. **Read tasklist.md**
   ```
   Read('.steering/[date]-[feature-name]/tasklist.md')
   ```

2. **Check for incomplete tasks (`[ ]`)**
   - Are all tasks `[x]`?
   - Is even a single `[ ]` left?

3. **If incomplete tasks are found**

   **❌ What NOT to do**:
   - Write "planned as a separate task due to time constraints" in the retrospective
   - Ignore incomplete tasks and proceed to the next step

   **✅ Correct ways to handle it**:

   **Pattern A: Implement the task**
   ```
   Return to step 3 (the task loop) and implement the incomplete task
   ```

   **Pattern B: If the task is too large**
   ```
   1. Split the task into smaller subtasks
   2. Add the split subtasks to tasklist.md
   3. Complete the subtasks one at a time
   ```

   **Pattern C: Only when the task has become unnecessary for technical reasons**

   Skipping is allowed only when one of the following technical reasons applies:
   - A change in implementation approach made the feature itself unnecessary
   - An architecture change replaced it with a different implementation method
   - A dependency change made the task impossible to execute

   Skip procedure:
   ```
   1. Note the technical reason in tasklist.md:
      "- [x] ~~Task name~~ (No longer needed due to implementation approach change: describe the specific technical reason in detail)"
   2. Record the reason for the change in detail in the retrospective section
   3. Clearly describe why this task became unnecessary and what replaced it
   ```

4. **Proceed only after confirming all tasks are complete**
   ```
   Confirm that all tasks are `[x]` before proceeding to step 5
   ```

#### Step 5: After all tasks are complete

1. **Final check**
   ```
   Read('.steering/[date]-[feature-name]/tasklist.md')
   ```

   Confirm that all tasks are `[x]`.

2. **Record in the retrospective section**
   ```
   Use the Edit tool to update the "Post-Implementation Retrospective" section of tasklist.md:
   - Implementation completion date
   - Differences between plan and actual results
   - Lessons learned
   - Improvement suggestions for next time
   ```

### Self-Check During Implementation

Every 5 tasks, verify the following:

- [ ] Has tasklist.md been updated recently? (within the last 5 tasks)
- [ ] Is the progress reflected in the document? (verify with the Read tool)
- [ ] Can the user understand the progress by looking at tasklist.md?

## Mode 3: Retrospective

### Purpose
After implementation is complete, record a retrospective in tasklist.md.

### Steps

1. **Read tasklist.md**
   ```
   Read('.steering/[date]-[feature-name]/tasklist.md')
   ```

2. **Compose the retrospective content**
   - Implementation completion date
   - Differences between plan and actual results (points that differed from the plan)
   - Lessons learned (technical learnings, process improvements)
   - Improvement suggestions for next time

3. **Update with the Edit tool**
   ```
   Update the "Post-Implementation Retrospective" section of tasklist.md
   ```

4. **Report to the user**
   ```
   "The retrospective has been recorded in tasklist.md. Please review the content."
   ```

## Troubleshooting

### If you forgot to update tasklist.md

If you notice during implementation that you forgot to update tasklist.md:

1. **Update immediately**
   ```
   Read('.steering/[date]-[feature-name]/tasklist.md')
   Identify the completed tasks and update them all to `[x]` with the Edit tool
   ```

2. **Report to the user**
   ```
   "Updates to tasklist.md were delayed, so I have now reflected the current progress."
   ```

3. **Prevent recurrence**
   - Update reliably starting with the next task
   - Strictly perform the self-check every 5 tasks

### Divergence between tasklist.md and the implementation

If the plan and the implementation diverge significantly:

1. **Add an annotation to tasklist.md**
   ```
   Use the Edit tool to add an annotation to the relevant task:
   "- [x] Task name (implementation method changed: reason)"
   ```

2. **Add new tasks if necessary**
   ```
   Use the Edit tool to add new tasks
   ```

3. **Update design.md as well**
   ```
   If the design change is significant, also update design.md
   ```

## Checklist (Most Important)

Always verify before implementation:

- [ ] Did you read tasklist.md?
- [ ] Did you identify the next task?
- [ ] Did you update with the Edit tool when starting the task?

Always verify after implementation:

- [ ] Did you update with the Edit tool when completing the task?
- [ ] Did you check the progress in tasklist.md?
- [ ] Is the state such that the user can understand the progress at a glance?

## Effects of This Skill

When this skill is used correctly:

- ✅ tasklist.md always reflects the latest progress
- ✅ The user can grasp the progress at a glance
- ✅ There is no divergence between documents and implementation
- ✅ Retrospectives become easier, leading to improvements next time
- ✅ A valuable record remains as project history

## Important Reminder

🚨 **The most important role of this skill is to reliably manage the progress of tasklist.md.**

- TodoWrite is a volatile note (not visible to the user)
- **tasklist.md is the persistent document (the one the user sees)**

While implementing, constantly ask yourself: "Can the user understand the progress when they look at tasklist.md?"
