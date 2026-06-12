---
name: glossary-creation
description: Detailed guide and template for creating a glossary. Use only when creating a glossary.
allowed-tools: Read, Write
---

# Glossary Creation Skill

This skill is a detailed guide for systematically defining project-specific terms and technical terms.

## Prerequisites

Before starting glossary creation, confirm the following:

### Recommended Documents

1. **docs/product-requirements.md** (PRD)
2. **docs/functional-design.md** (Functional Design Document)
3. **docs/architecture.md** (Architecture Design Document)
4. **docs/repository-structure.md** (Repository Structure)
5. **docs/development-guidelines.md** (Development Guidelines)

The glossary defines the terms used across all documents in a unified way.
Extract terms from each document and organize them systematically.

## Priority of Existing Documents

**Important**: If an existing glossary exists at `docs/glossary.md`,
follow this order of priority:

1. **Existing glossary (`docs/glossary.md`)** - Highest priority
   - Contains project-specific term definitions
   - Takes precedence over this skill's guide

2. **This skill's guide** - Reference material
   - Generic templates and examples
   - Use when no existing glossary exists, or as a supplement

**When creating a new document**: Refer to this skill's template and guide
**When updating**: Update while preserving the structure and content of the existing glossary

## Output Location

Save the completed glossary to:

```
docs/glossary.md
```

## Template Reference

When creating the glossary, use the following template: ./template.md

## Detailed Guide

For a more detailed creation guide, refer to: ./guide.md
