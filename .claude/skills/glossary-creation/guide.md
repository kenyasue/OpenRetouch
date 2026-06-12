# Glossary Creation Guide

## Basic Principles

### 1. Clear and Consistent Definitions

Eliminate ambiguity from term definitions so that every reader reaches the same understanding.

**Bad example**:
```markdown
## Task
Something the user has to do
```

**Good example**:
```markdown
## Task
**Definition**: A unit of work that the user must complete. Has a title, description,
due date, and status (todo / in progress / completed).

**Related terms**: Subtask, Task Group

**Usage examples**:
- "Add a task": register a new task in the system
- "Complete a task": change the task's status to completed

**Data model**: `src/types/Task.ts`
```

### 2. Include Concrete Examples

Provide concrete usage examples, not just abstract definitions.

**Example**:
```markdown
## Priority
**Definition**: A three-level indicator of a task's importance and urgency

**Value definitions**:
- `high`: Urgent and important. Requires immediate attention
- `medium`: Important but not urgent. Handle in a planned manner
- `low`: Low in both importance and urgency. Handle when time permits

**Decision criteria**:
- high: due within 24 hours, or blocking other tasks
- medium: due within one week
- low: due more than one week out, or no due date

**Usage example**:
```typescript
const task: Task = {
  title: 'Fix security vulnerability',
  priority: 'high', // Requires urgent attention
};
```
```

### 3. Link Related Terms

Make the relationships between terms explicit.

**Example**:
```markdown
## Task
**Definition**: [Definition]

**Related terms**:
- [Subtask](#subtask): a task broken down into smaller pieces
- [Task Group](#task-group): a collection of multiple tasks
- [Status](#task-status): the progress state of a task

**Parent-child relationships**:
- Parent: Task Group
- Children: Subtasks
```

## How to Categorize Terms

### Defining Domain Terms

**Scope**: Project-specific business concepts

**Items to define**:
```markdown
## [Term]

**Definition**: [Concise definition in 1-2 sentences]

**Description**: [Detailed description, background, constraints]

**Related terms**: [Other related terms]

**Usage examples**: [Concrete usage scenarios]

**Data model**: [Relevant file path]

**English notation**: [English term] (if the project has global reach)
```

**Example**:
```markdown
## Steering File

**Definition**: A temporary document created for short-term task management

**Description**:
Steering files are placed in the `.steering/[YYYYMMDD]-[task-name]/` directory
and are deleted 1-2 weeks after the task is completed. They contain the task
specification, implementation notes, review records, and so on.

**Related terms**:
- [Persistent Document](#persistent-document): documents preserved long-term
- [Task Mode](#task-mode): the development mode that uses steering files

**Usage examples**:
- "Create a steering file for the feature addition"
- "Delete or archive the steering file after the task is completed"

**Directory structure**:
```
.steering/
└── 20250101-add-priority-feature/
    ├── requirements.md      # Requirements for this piece of work
    ├── design.md            # Design of the changes
    └── tasklist.md          # Task list
```

**English notation**: Steering File
```

### Defining Technical Terms

**Scope**: Technologies, frameworks, and tools in use

**Items to define**:
```markdown
## [Technology name]

**Definition**: [Concise description of the technology]

**Official site**: [URL]

**Usage in this project**: [How it is used]

**Version**: [Version in use]

**Reason for selection**: [Why this technology was chosen]

**Alternatives**: [Other options that were considered]

**Related documents**: [Links to internal documents]

**Configuration file**: [Path to the configuration file]
```

**Example**:
```markdown
## TypeScript

**Definition**: A programming language that adds static typing to JavaScript

**Official site**: https://www.typescriptlang.org/

**Usage in this project**:
All source code is written in TypeScript to ensure type safety.

**Version**: 5.3.x

**Reason for selection**:
- Improved maintainability in large-scale development
- Improved development efficiency through editor completion
- Error detection at compile time

**Alternatives**:
- JavaScript ESM: does not provide the benefits of type checking
- Flow: inferior to TypeScript in ecosystem maturity

**Related documents**:
- [Architecture Design Document](./architecture.md#technology-stack)
- [Development Guidelines](./development-guidelines.md#typescript-standards)

**Configuration file**: `tsconfig.json`
```

### Defining Abbreviations and Acronyms

**Principles**:
- State the full name
- On first occurrence, write both the abbreviation and the full name
- Avoid project-specific abbreviations (use only common abbreviations)

**Example**:
```markdown
## CLI

**Full name**: Command Line Interface

**Meaning**: An interface operated from the command line

**Usage in this project**:
Used as the main interface of the Devtask tool. Users operate on tasks
with commands such as `devtask add "task"`.

**Implementation**: `src/cli/` directory

**Alternative interfaces**: A GUI version is under consideration as a future extension

## TDD

**Full name**: Test-Driven Development

**Meaning**: A development methodology in which tests are written before the implementation

**Application in this project**:
TDD is adopted for all new feature development.

**Procedure**:
1. Write a test
2. Run the test → confirm it fails
3. Write the implementation
4. Run the test → confirm it passes
5. Refactor

**Reference**: [Development Guidelines](./development-guidelines.md#TDD)
```

### Defining Architecture Terms

**Scope**: Concepts related to system design and patterns

**Items to define**:
```markdown
## [Concept]

**Definition**: [Description of the architectural concept]

**Application in this project**: [Concrete implementation approach]

**Advantages**: [Reasons for adoption]

**Disadvantages**: [Constraints and trade-offs]

**Related components**: [Related components]

**Diagram**: [Structure diagram]

**References**: [Reference literature or URLs]
```

**Example**:
```markdown
## Layered Architecture

**Definition**: A design pattern that divides a system into multiple layers by role,
with one-directional dependencies from upper layers to lower layers

**Application in this project**:
A 3-layer architecture is adopted:

```
UI layer (cli/)
    ↓
Service layer (services/)
    ↓
Data layer (repositories/)
```

**Responsibilities of each layer**:
- UI layer: accept and display user input
- Service layer: implement business logic
- Data layer: persist and retrieve data

**Advantages**:
- Improved maintainability through separation of concerns
- Easy to test (each layer can be tested independently)
- The impact scope of changes is limited

**Disadvantages**:
- May be over-engineering for small projects
- Overhead from data conversion between layers

**Dependency rules**:
- ✅ UI layer → Service layer
- ✅ Service layer → Data layer
- ❌ Data layer → Service layer
- ❌ Data layer → UI layer

**Implementation location**: Reflected in the structure of the `src/` directory

**References**:
- [Architecture Design Document](./architecture.md)
- [Repository Structure Document](./repository-structure.md)
```

## Defining State Transitions

**Scope**: Entity statuses and states

**Definition method**:

1. **Enumerate in table form**
2. **State transition conditions explicitly**
3. **Visualize with a Mermaid diagram**

**Example**:
```markdown
## Task Status

**Definition**: An enum representing the progress state of a task

**Possible values**:

| Status | Meaning | Transition Condition | Next State |
|----------|------|---------|---------|
| `todo` | Not started | Initial state when a task is created | `in_progress` |
| `in_progress` | In progress | User starts the task | `completed`, `todo` |
| `completed` | Completed | User completes the task | `todo` (can be reopened) |

**State transition diagram**:
```mermaid
stateDiagram-v2
    [*] --> todo: Task created
    todo --> in_progress: Work started
    in_progress --> completed: Completed
    in_progress --> todo: Suspended
    completed --> todo: Reopened
    completed --> [*]: Archived
```

**Implementation**:
```typescript
// src/types/Task.ts
export type TaskStatus = 'todo' | 'in_progress' | 'completed';

// Validating state transitions
function canTransition(
  from: TaskStatus,
  to: TaskStatus
): boolean {
  const validTransitions: Record<TaskStatus, TaskStatus[]> = {
    todo: ['in_progress'],
    in_progress: ['completed', 'todo'],
    completed: ['todo'],
  };
  return validTransitions[from].includes(to);
}
```

**Business rules**:
- Direct transition from `todo` to `completed` is forbidden
- Completed tasks can be reopened
- Archived tasks cannot be modified
```

## Defining Errors and Exceptions

**Scope**: Error classes defined in the system

**Items to define**:
```markdown
## [Error name]

**Class name**: `[ErrorClassName]`

**Inherits from**: `Error` or `[ParentError]`

**Occurrence conditions**: [When it occurs]

**Error message format**: [Message format]

**Remediation**:
- User: [What the user should do]
- Developer: [What the developer should do]

**Error code**: [If applicable]

**Log level**: [ERROR, WARN, INFO]

**Implementation location**: [File path]

**Usage example**: [Code example]
```

**Example**:
```markdown
## Validation Error

**Class name**: `ValidationError`

**Inherits from**: `Error`

**Occurrence conditions**:
Occurs when user input violates a business rule.

**Error message format**:
```
[Field name]: [Error description]
```

**Remediation**:
- User: correct the input according to the error message
- Developer: verify that the validation logic is correct

**Error code**: `VAL-XXX` (XXX is a 3-digit number)

**Log level**: WARN (because the error is user-induced)

**Implementation location**: `src/errors/ValidationError.ts`

**Usage example**:
```typescript
// Throwing the error
if (title.length === 0) {
  throw new ValidationError(
    'Title is required',
    'title',
    title
  );
}

// Handling the error
try {
  await taskService.create(data);
} catch (error) {
  if (error instanceof ValidationError) {
    console.error(`Input error: ${error.message}`);
    console.error(`Field: ${error.field}`);
  }
}
```

**Related validations**:
- Title: 1-200 characters
- Due date: must be in the future
- Priority: one of high, medium, low
```

## Maintaining and Updating Terms

### When to Add Terms

**Add when**:
- A new concept has been introduced
- A team member has asked about a term
- A term appears three or more times in the documentation
- An external service or API has been integrated

**No need to add when**:
- The term is general programming vocabulary (variable, function, etc.)
- The term is temporary and used only once

### Update Workflow

1. **Add or change the term**
   - Add it to the appropriate category
   - Fill in all the definition items
   - Link related terms

2. **Review**
   - Share with team members
   - Verify the validity of the definition

3. **Record the change history**
   - Update the glossary's change history table
   - Note the change in the commit message

4. **Check the impact scope**
   - Search for places where the term is used
   - Update documents as needed

### Managing the Index

**Organize in alphabetical order**:

```markdown
## Index

### A
- [Archive](#archive) - Process term

### C
- [CLI](#CLI) - Abbreviation
- [Coverage](#coverage) - Technical term

### E
- [Error Handling](#error-handling) - Technical term

### S
- [Status](#task-status) - Data model term
- [Steering File](#steering-file) - Domain term

### T
- [Task](#task) - Domain term
- [TDD](#TDD) - Abbreviation
- [TypeScript](#TypeScript) - Technical term
```

## Checklist

- [ ] All terms are clearly defined
- [ ] Concrete examples are included
- [ ] Related terms are linked
- [ ] Categories are appropriately organized
- [ ] Technical terms include version information
- [ ] Abbreviations include their full names
- [ ] State transitions are illustrated
- [ ] Errors include remediation steps
- [ ] The index is organized
- [ ] The change history is recorded
