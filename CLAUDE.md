# Project Memory

## Tech Stack

- Development environment: devcontainer
- Node.js v24.11.0
- TypeScript 5.x
- Package manager: npm

## Core Principles of Spec-Driven Development

### Basic Flow

1. **Document creation**: Define "what to build" in the persistent documents (`docs/`)
2. **Work planning**: Plan "what to do this time" in steering files (`.steering/`)
3. **Implementation**: Implement according to tasklist.md, updating progress as you go
4. **Verification**: Run tests and confirm behavior
5. **Update**: Update documents as needed

### Important Rules

#### When creating documents

**Create one file at a time, and always obtain the user's approval before moving on to the next**

When waiting for approval, state it clearly:
```
"Creation of [document name] is complete. Please review the content.
Once approved, I will proceed to the next document."
```

#### Checks before implementation

Before starting any new implementation, always confirm the following:

1. Read CLAUDE.md
2. Read the related persistent documents (`docs/`)
3. Search for existing similar implementations with Grep
4. Understand existing patterns before starting implementation

#### Steering file management

Create `.steering/[YYYYMMDD]-[task name]/` for each piece of work:

- `requirements.md`: Requirements for this work
- `design.md`: Implementation approach
- `tasklist.md`: Concrete task list

Naming convention: `20250115-add-user-profile` format

#### Managing steering files

**Use the `steering` skill for work planning, implementation, and verification.**

- **When planning work**: Use `Skill('steering')` in mode 1 (steering file creation)
- **When implementing**: Use `Skill('steering')` in mode 2 (implementation and tasklist.md update management)
- **When verifying**: Use `Skill('steering')` in mode 3 (retrospective)

Detailed procedures and update-management rules are defined inside the steering skill.

## Directory Structure

### Persistent documents (`docs/`)

Define "what to build" and "how to build it" for the application as a whole:

#### Drafts and ideas (`docs/ideas/`)
- Output of brainstorming and idea-bouncing sessions
- Technical research notes
- Free-form (minimal structure)
- Automatically loaded when `/setup-project` is run

#### Official documents
- **product-requirements.md** - Product Requirements Document
- **functional-design.md** - Functional Design Document
- **architecture.md** - Technical Specification
- **repository-structure.md** - Repository Structure Definition
- **development-guidelines.md** - Development Guidelines
- **glossary.md** - Ubiquitous Language Definition

### Per-work documents (`.steering/`)

Define "what to do this time" for a specific piece of development work:

- `requirements.md`: Requirements for this work
- `design.md`: Design of the changes
- `tasklist.md`: Task list

## Development Process

### Initial setup

1. Use this template
2. Run `/setup-project` to create the persistent documents (creates 6 interactively)
3. Run `/add-feature [feature]` to implement features

### Day-to-day usage

**By default, just make requests in normal conversation:**

```bash
# Editing documents
> Add a new feature to the PRD
> Review the performance requirements in architecture.md
> Add a new domain term to glossary.md

# Adding features (use the command for the standard flow)
> /add-feature user profile editing

# Detailed review (when a detailed report is needed)
> /review-docs docs/product-requirements.md
```

**Key point**: You do not need to be aware of the details of spec-driven development. Claude Code will determine and load the appropriate skill.

## Document Management Principles

### Persistent documents (`docs/`)

- Describe the fundamental design
- Not updated frequently
- The "north star" for the entire project

### Per-work documents (`.steering/`)

- Specific to a particular piece of work
- Created anew for each piece of work
- Kept as history
