---
name: prd-writing
description: Detailed guide and template for creating a Product Requirements Document (PRD). Use only when creating a PRD.
allowed-tools: Read, Write
---

# PRD Writing Skill

This skill is a detailed guide for creating a high-quality Product Requirements Document (PRD).

## Prerequisites

Before starting PRD creation, the following must be complete:

### Idea brainstorming is complete

The user must have finished refining the product idea through dialogue with Claude Code.

### docs/ideas/initial-requirements.md has been created

**Important**: The user must save the brainstorming results in the following file:

**File path**: `docs/ideas/initial-requirements.md`

This file must contain the following content:
- The basic product idea
- The problem to be solved
- An overview of the target users
- The main features to implement
- The scope of the MVP

When creating the PRD, refer to the contents of this file and elaborate on them.

## Priority of Existing Documents

**Important**: If an existing PRD exists at `docs/product-requirements.md`,
follow this order of priority:

1. **The existing PRD (`docs/product-requirements.md`)** - highest priority
   - Contains project-specific requirements
   - Takes precedence over this skill's guide

2. **This skill's guide** - reference material
   - Generic template and examples
   - Use when there is no existing PRD, or as a supplement

**When creating new**: Refer to this skill's template and guide
**When updating**: Update while preserving the structure and content of the existing PRD

## Output Location

Save the created PRD to:

```
docs/product-requirements.md
```

## Template Reference

When creating a PRD, use the following template: ./template.md

## PRD Creation Process

### 1. Review initial-requirements.md

First, review the initial requirements specification created by the user:

```bash
Read('docs/ideas/initial-requirements.md')
```

### 2. Generate the PRD draft

Based on the contents of initial-requirements.md, generate the PRD following the template.

### 3. Review and improve the PRD

Review the generated PRD from the following perspectives:

#### Review Perspectives

1. Is the product vision clear?
2. Are the target users specific?
3. Are the success metrics measurable?
4. Are the functional requirements detailed to an implementable level?
5. Are the non-functional requirements comprehensive?

#### Evaluation Criteria for Review Results

Evaluate the generated PRD in the following format:

**✅ Strengths**
- A clear vision is described in a measurable, concrete way
- Functional specifications are detailed down to the implementation level
- KPIs are defined with quantitative metrics

**⚠️ Areas Needing Improvement**

Ambiguity in functional requirements:
- Problem: Some areas lack a clear, concrete implementation specification
- Recommendation: Explicitly state concrete command specifications and error handling

Measurement methods for success metrics:
- Problem: The measurement method is unclear
- Recommendation: Explicitly state the measurement method and privacy considerations

### 4. Improve after the review

Go through the issues identified in the review one by one and improve the areas that need to be made more concrete:

1. Review each identified issue one by one
2. Improve the areas that need to be made more concrete
3. After improving, run the review again
4. Repeat until no issues remain

**Notes**:
- Do not accept AI reviews at face value; the final judgment must always be made by a human
- Specify the review perspectives explicitly
- A human must verify the validity of improvement suggestions

## Key Points for PRD Creation

### 1. Specificity and Measurability

All requirements must be specific and measurable.

**Bad examples**:
- The system must be fast
- Users find it easy to use

**Good examples**:
- Command execution time: within 100ms (on an average PC environment)
- New users can learn basic operations within 5 minutes (measured via usability testing)

### 2. User-Centered Design

Every feature must solve a clearly identified user problem.

**User story format**:
```
As a [user], I want [feature] in order to [goal]
```

**Example**:
```
As a developer, I want a CLI-based task management tool
so that I can manage tasks without leaving the terminal
```

### 3. Clear Prioritization

Assign a priority to every feature:

- **P0 (Must-have)**: Features included in the MVP (Minimum Viable Product). The product is not viable without these
- **P1 (Important)**: Features that should be added soon after the initial release
- **P2 (Nice-to-have)**: Features to consider adding in the future

## Details of the Main PRD Sections

### 1. Product Overview

#### Components

1. **Name**: Product name and subtitle
2. **Product concept**: Three main concepts
3. **Product vision**: The envisioned world in 3-5 sentences
4. **Goals**: A list of concrete goals

#### Example

```markdown
### Name
**Devtask** - A task management CLI tool for developers

### Product Concept
- Task management completed entirely in the CLI: complete every operation without leaving the terminal
- Automatic priority estimation: automatically estimate priority from task deadlines, creation dates, status change history, and more
- Simple, fast user experience: complete operations with minimal keystrokes and instant responses

### Product Vision
Provide a CLI tool that lets developers manage tasks efficiently without leaving the terminal.
Specialized for command-line operation, it delivers lightweight, fast task management that does not interrupt the development flow.
With automatic priority estimation, developers can focus on their essential work.
```

**Include a concrete value proposition**

Bad example:
```
Build a convenient task management tool
```

Good example:
```
A CLI tool that lets developers manage tasks without leaving the terminal.

Value provided:
- Reduced context switching (zero switching between GUI and terminal)
- Improved work efficiency (no mouse required, average 30% time savings)
- Integration with automation (can be embedded in shell scripts)
```

### 2. Target Users (Personas)

#### Required Elements

1. **Basic attributes**: Age, occupation, years of experience
2. **Tech stack**: Tools and languages they use
3. **Current problems**: Concrete pain points
4. **Expected solution**: What they want to achieve
5. **Typical daily workflow**

#### Example

```markdown
### Primary Persona: Taro Tanaka (29, full-stack engineer)
- Freelancer juggling 3-5 projects in parallel
- Vim/Emacs + terminal environment
- Does not want to spend time on task management
- Prefers Markdown, Git, and CLI tools
```

### 3. Success Metrics (KPIs)

#### SMART Principles

- **S**pecific: Clear about what is being measured
- **M**easurable: Can be measured numerically
- **A**chievable: A realistic goal
- **R**elevant: Tied to business goals
- **T**ime-bound: Has a deadline for achievement

#### Example

```markdown
### Primary KPIs
- Daily active users (DAU): 100 (within 3 months)
- Task completion rate: 70% or higher
- Average command executions per day: 10 or more
```

### 4. Functional Requirements

#### Core Features (MVP)

Include the following for each feature:
- User story
- Acceptance criteria (checklist format)
- Priority (P0/P1/P2)

**Format**:
```markdown
### [Feature name]

User story:
As a [user], I want [feature] in order to [goal]

Acceptance criteria:
- [ ] Condition 1 (measurable)
- [ ] Condition 2 (measurable)

Priority: P0 (must-have) / P1 (important) / P2 (nice-to-have)
```

#### CLI Interface

For CLI tools, include concrete command examples:

```bash
# Basic operations
devtask add "Task name" --due 2025-01-15 --priority high
devtask list
devtask next  # Show the task to do now
devtask done <task-id>
devtask show <task-id>
```

### 5. Non-Functional Requirements

Describe them in a measurable form:

**Example**:
```markdown
### Performance
- Command execution time: within 100ms (on an average PC environment)
- Task list display: within 1 second for up to 1000 items

### Usability
- New users can learn basic operations within 5 minutes
- All features can be discovered via the help command

### Reliability
- Zero data loss (automatic backups)
- Rollback on errors
```

## Quality Standards and Checkpoints

To ensure PRD quality, verify the following checkpoints:

### Vision and Goals
- [ ] Is the product vision clear and measurable?
- [ ] Is the concrete value provided defined?
- [ ] Is the target market clear?

### Target Users
- [ ] Are the personas defined concretely?
- [ ] Are the current problems and expected solutions clear?
- [ ] Are the tech stack and daily workflow described?

### Success Metrics
- [ ] Are KPIs defined following the SMART principles?
- [ ] Is the measurement method clear?
- [ ] Are deadlines for achievement set?

### Functional Requirements
- [ ] Are all features written in user story format?
- [ ] Are acceptance criteria defined in a measurable form?
- [ ] Are priorities (P0/P1/P2) clearly assigned?

### Non-Functional Requirements
- [ ] Are performance criteria defined with concrete numbers?
- [ ] Are usability criteria measurable?
- [ ] Are reliability and security requirements clear?

## Summary

Keys to successful PRD creation:

1. **Build on initial-requirements.md**: Refer to the brainstorming content created by the user
2. **Specificity and measurability**: Make all requirements explicit
3. **User-centered**: Only features that solve user problems
4. **Clear prioritization**: Classify as P0/P1/P2
5. **Review and improvement**: Self-review plus final human judgment
6. **Apply the SMART principles**: Especially important when defining KPIs
