# Verbose Output Control

The Schema-Guided Reasoner now supports two output modes: **Verbose** and **Concise**.

## Selecting Output Mode

When you start the application, you'll be prompted:

```
Enable verbose output? (Shows detailed JSON responses and debug information) [y/n] (n):
```

- Press `n` (default) for **concise output** - recommended for normal use
- Press `y` for **verbose output** - useful for debugging and understanding the reasoning process

## Output Comparison

### Concise Mode (Default - `VerboseOutput = false`)

**What you see:**
- **Planned remaining steps** (always shown - helps understand LLM's reasoning)
- Step number and selected tool
- Brief description of action
- Result summary
- Final completion message

**Example output:**
```
  Planned remaining steps:
    1. Send the email to john@example.com with provided subject and body.
    2. Confirm email was sent successfully
Step 1: SendEmail - Send the email to john@example.com with provided subject and body.
  → Email sent successfully to john@example.com
✓ Completed: Email sent to john@example.com
```

**Benefits:**
- Clean, easy-to-read output
- **Shows LLM's planning and multi-step reasoning** (valuable insight)
- Focuses on what's happening, not how
- Better for production use
- Faster to scan and understand

---

### Verbose Mode (`VerboseOutput = true`)

**What you see:**
- Step planning indicators
- LLM call numbers
- Raw JSON responses from the LLM
- Full planned steps list
- Next action details
- Selected tool with full parameter JSON
- Execution results

**Example output:**
```
Planning step_1... (LLM call #1)
Assistant raw response:
{"CurrentState":"User requested to send a welcome email...","PlanRemainingStepsBrief":["Send the email..."],"TaskCompleted":false,"NextStepToolToCall":{"Summary":"Prepare SendEmail","type":"send_email_tool_call","Subject":"Welcome","Message":"Thank you for joining us!","RecipientEmail":"john@example.com","Files":[]}}
  Planned remaining steps:
    1. Send the email to john@example.com with provided subject and body.
  → Next action: Send the email to john@example.com with provided subject and body.
  Selected tool: SendEmail
{
  "Summary": "Prepare SendEmail tool call",
  "type": "send_email_tool_call",
  "Subject": "Welcome",
  "Message": "Thank you for joining us!",
  "RecipientEmail": "john@example.com",
  "Files": []
}
    ✓ Email sent successfully to john@example.com
Task completed: Email sent to john@example.com
```

**Benefits:**
- Full transparency into LLM reasoning
- See exact JSON schema responses
- Understand step-by-step planning
- Debug schema validation issues
- Verify tool parameters

---

## Programmatic Control

You can also set the output mode programmatically:

```csharp
var reasoner = new SchemaGuidedReasoner(kernel, databaseService)
{
    VerboseOutput = false  // Set to true for verbose mode
};
```

## When to Use Each Mode

### Use Concise Mode When:
- Running the application in production
- You understand how the system works
- You want clean, readable output
- You're demonstrating to non-technical users
- You want to focus on results, not process

### Use Verbose Mode When:
- Debugging schema generation issues
- Understanding LLM reasoning patterns
- Verifying tool parameter serialization
- Learning how Schema-Guided Reasoning works
- Troubleshooting unexpected behavior
- Developing new business functions

## Default Setting

The default is **Concise Mode** (`VerboseOutput = false`) because:
- Most users prefer clean output
- It's more production-ready
- Easier to read and understand
- Reduces information overload
- Better for demos and presentations

You can always enable verbose mode when you need to dig deeper into the reasoning process.
