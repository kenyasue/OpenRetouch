---
description: Run a detailed document review using a subagent
---

# Document Review

Argument: document path (e.g., `/review-docs docs/product-requirements.md`)

## How to Run

```bash
claude
> /review-docs docs/product-requirements.md
```

## Procedure

### Step 1: Confirm the document exists

Confirm that the specified document exists.

### Step 2: Launch the doc-reviewer subagent

Launch the doc-reviewer subagent to perform the review:

Use the Task tool to launch the doc-reviewer subagent:
- subagent_type: "doc-reviewer"
- description: "Document detailed review"
- prompt: "Review [document path] in detail.\n\nEvaluate it from the following perspectives:\n1. Completeness: Are all required items included?\n2. Specificity: Are there any ambiguous expressions?\n3. Consistency: Is it consistent with the other documents?\n4. Measurability: Are the success metrics measurable (for a PRD)?\n\nProduce a review report."

### Step 3: Summarize the review results

Extract the key points from the review report produced by the subagent and report them to the user.

## Output Format

```markdown
# Document Review Results

## Document: [file name]

### Main Improvement Points

1. [improvement 1] (priority: high/medium/low)
2. [improvement 2] (priority: high/medium/low)
3. [improvement 3] (priority: high/medium/low)

### Overall Evaluation

[1-5]/5

### Next Actions

- [recommended action 1]
- [recommended action 2]

For the detailed report, refer to the subagent's output.
```

## Notes

- The review may take several minutes because it performs detailed analysis
- The subagent runs in an independent context, so it does not consume the main agent's context
