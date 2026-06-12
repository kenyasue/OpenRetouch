---
description: "Initial setup: interactively create the 6 persistent documents"
---

# Initial Project Setup

This command interactively creates the project's 6 persistent documents.

## How to Run

```bash
claude
> /setup-project
```

## Pre-execution Check

Check the files in the `docs/ideas/` directory.
```bash
# Check
ls docs/ideas/

# If files exist
✅ docs/ideas/initial-requirements.md was found
   The PRD will be created based on this content

# If no files exist
⚠️  No files in docs/ideas/
   The PRD will be created interactively
```

## Procedure

### Step 0: Load the input

1. Read all Markdown files in `docs/ideas/`
2. Understand the content and use it as reference for creating the PRD

### Step 1: Create the Product Requirements Document

1. Load the **prd-writing skill**
2. Create `docs/product-requirements.md` based on the content of `docs/ideas/`
3. Flesh out the ideas from the brainstorming sessions:
   - Detailed user stories
   - Acceptance criteria
   - Non-functional requirements
   - Success metrics
4. Ask the user for confirmation and **wait until approved**

**Since the subsequent steps are based on the content of the Product Requirements Document, create them automatically**

### Step 2: Create the Functional Design Document

1. Load the **functional-design skill**
1. Read `docs/product-requirements.md`
3. Create `docs/functional-design.md` following the skill's template and guide

### Step 3: Create the Architecture Design Document

1. Load the **architecture-design skill**
2. Read the existing documents
3. Create `docs/architecture.md` following the skill's template and guide

### Step 4: Create the Repository Structure Definition

1. Load the **repository-structure skill**
2. Read the existing documents
3. Create `docs/repository-structure.md` following the skill's template

### Step 5: Create the Development Guidelines

1. Load the **development-guidelines skill**
2. Read the existing documents
3. Create `docs/development-guidelines.md` following the skill's template

### Step 6: Create the Glossary

1. Load the **glossary-creation skill**
2. Read the existing documents
3. Create `docs/glossary.md` following the skill's template

## Completion Criteria

- All 6 persistent documents have been created

Message upon completion:
```
"Initial setup is complete!

Documents created:
✅ docs/product-requirements.md
✅ docs/functional-design.md
✅ docs/architecture.md
✅ docs/repository-structure.md
✅ docs/development-guidelines.md
✅ docs/glossary.md

You are now ready to start development.

How to use going forward:
- Editing documents: just make requests in normal conversation
  e.g., "Add a new feature to the PRD", "Review architecture.md"

- Adding features: run /add-feature [feature name]
  e.g., /add-feature user authentication

- Document review: run /review-docs [path]
  e.g., /review-docs docs/product-requirements.md
"
```
