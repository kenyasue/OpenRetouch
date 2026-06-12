# Task List

## 🚨 Principle of Complete Task Completion

**Continue working until all tasks in this file are complete**

### Mandatory Rules
- **Mark every task as `[x]`**
- "Planned as a separate task due to time constraints" is prohibited
- "Deferred because the implementation is too complex" is prohibited
- Do not end work while incomplete tasks (`[ ]`) remain

### Plan Only Implementable Tasks
- During planning, list only tasks that can actually be implemented
- Do not include "tasks we might do in the future"
- Do not include "tasks under consideration"

### The Only Cases Where Skipping a Task Is Permitted
Skipping is allowed only when one of the following technical reasons applies:
- A change in implementation approach made the feature itself unnecessary
- An architecture change replaced it with a different implementation method
- A dependency change made the task impossible to execute

When skipping, always state the reason:
```markdown
- [x] ~~Task name~~ (No longer needed due to implementation approach change: specific technical reason)
```

### If a Task Is Too Large
- Split the task into smaller subtasks
- Add the split subtasks to this file
- Complete the subtasks one at a time

---

## Phase 1: {Phase name}

- [ ] {Task 1}
  - [ ] {Subtask 1-1}
  - [ ] {Subtask 1-2}

- [ ] {Task 2}
  - [ ] {Subtask 2-1}
  - [ ] {Subtask 2-2}

## Phase 2: {Phase name}

- [ ] {Task 1}
  - [ ] {Subtask 1-1}
  - [ ] {Subtask 1-2}

- [ ] {Task 2}

## Phase 3: Quality Checks and Fixes

- [ ] Confirm all tests pass
  - [ ] `npm test`
- [ ] Confirm there are no lint errors
  - [ ] `npm run lint`
- [ ] Confirm there are no type errors
  - [ ] `npm run typecheck`
- [ ] Confirm the build succeeds
  - [ ] `npm run build`

## Phase 4: Documentation Updates

- [ ] Update README.md (if necessary)
- [ ] Post-implementation retrospective (record at the bottom of this file)

---

## Post-Implementation Retrospective

### Implementation Completion Date
{YYYY-MM-DD}

### Differences Between Plan and Actual Results

**Points that differed from the plan**:
- {Technical changes that were not anticipated at planning time}
- {Changes to the implementation approach and their reasons}

**Newly required tasks**:
- {Tasks added during implementation}
- {Why the addition was necessary}

**Tasks skipped for technical reasons** (only if applicable):
- {Task name}
  - Reason for skipping: {Specific technical reason}
  - Alternative implementation: {What replaced it}

**⚠️ Note**: Do not list tasks skipped for reasons such as "time constraints" or "too difficult" here. Completing all tasks is the principle.

### Lessons Learned

**Technical learnings**:
- {Technical insights gained through the implementation}
- {New technologies or patterns used}

**Process improvements**:
- {What went well in task management}
- {How the steering files were utilized}

### Improvement Suggestions for Next Time
- {Things to watch out for in the next feature addition}
- {More efficient implementation methods}
- {Improvements to task planning}
